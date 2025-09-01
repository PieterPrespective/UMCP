using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace UMCP.Editor
{
    // Context class to track which fields were set during deserialization
    public class DeserializationTracker
    {
        private readonly ConcurrentDictionary<string, HashSet<string>> _setFields = new();

        public string CreateTrackingKey()
        {
            var key = Guid.NewGuid().ToString();
            _setFields[key] = new HashSet<string>();
            return key;
        }

        public void MarkFieldAsSet(string key, string fieldPath)
        {
            if (_setFields.TryGetValue(key, out var fields))
            {
                lock (fields)
                {
                    fields.Add(fieldPath);
                }
            }
        }

        public HashSet<string> GetSetFields(string key)
        {
            return _setFields.TryGetValue(key, out var fields) ? new HashSet<string>(fields) : new HashSet<string>();
        }

        public void CleanupTracking(string key)
        {
            _setFields.TryRemove(key, out _);
        }
    }

    // Base converter that tracks field access
    public abstract class TrackingJsonConverter<T> : JsonConverter<T> where T : class
    {
        private static readonly DeserializationTracker Tracker = new();

        protected abstract T CreateInstance();
        protected abstract void PopulateInstance(T instance, JObject jsonObject, JsonSerializer serializer, string trackingKey);

        public override T ReadJson(JsonReader reader, Type objectType, T existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            // Get or create tracking key from context
            var trackingKey = GetTrackingKey(serializer);

            if (reader.TokenType == JsonToken.Null)
                return null;

            var jsonObject = JObject.Load(reader);
            var instance = existingValue ?? CreateInstance();

            // Track all fields that exist in the JSON
            TrackFieldsFromJson(jsonObject, trackingKey, "");

            // Populate the instance
            PopulateInstance(instance, jsonObject, serializer, trackingKey);

            return instance;
        }

        private void TrackFieldsFromJson(JObject jsonObject, string trackingKey, string basePath)
        {
            foreach (var property in jsonObject.Properties())
            {
                var fullPath = string.IsNullOrEmpty(basePath)
                    ? property.Name
                    : $"{basePath}.{property.Name}";

                Tracker.MarkFieldAsSet(trackingKey, fullPath);

                // Recursively track nested objects
                if (property.Value is JObject nestedObject)
                {
                    TrackFieldsFromJson(nestedObject, trackingKey, fullPath);
                }
                else if (property.Value is JArray array)
                {
                    TrackArrayFields(array, trackingKey, fullPath);
                }
            }
        }

        private void TrackArrayFields(JArray array, string trackingKey, string basePath)
        {
            for (int i = 0; i < array.Count; i++)
            {
                var itemPath = $"{basePath}[{i}]";
                Tracker.MarkFieldAsSet(trackingKey, itemPath);

                if (array[i] is JObject nestedObject)
                {
                    TrackFieldsFromJson(nestedObject, trackingKey, itemPath);
                }
                else if (array[i] is JArray nestedArray)
                {
                    TrackArrayFields(nestedArray, trackingKey, itemPath);
                }
            }
        }

        protected string GetTrackingKey(JsonSerializer serializer)
        {
            if (serializer.Context.Context is string key)
                return key;

            // Create a new key if none exists
            key = Tracker.CreateTrackingKey();
            serializer.Context = new System.Runtime.Serialization.StreamingContext(
                serializer.Context.State,
                key
            );
            return key;
        }

        protected HashSet<string> GetSetFields(string trackingKey)
        {
            return Tracker.GetSetFields(trackingKey);
        }

        protected void CleanupTracking(string trackingKey)
        {
            Tracker.CleanupTracking(trackingKey);
        }

        public override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
        {
            // Default serialization
            var jsonObject = JObject.FromObject(value, JsonSerializer.CreateDefault(
                new JsonSerializerSettings
                {
                    ContractResolver = serializer.ContractResolver,
                    Converters = serializer.Converters.Where(c => c != this).ToList()
                }
            ));
            jsonObject.WriteTo(writer);
        }
    }

    // Generic implementation for any class
    public class GenericTrackingConverter<T> : TrackingJsonConverter<T> where T : class, new()
    {
        protected override T CreateInstance()
        {
            return new T();
        }

        protected override void PopulateInstance(T instance, JObject jsonObject, JsonSerializer serializer, string trackingKey)
        {
            var type = typeof(T);
            var contract = serializer.ContractResolver.ResolveContract(type) as JsonObjectContract;

            foreach (var property in jsonObject.Properties())
            {
                var jsonProperty = contract?.Properties.GetClosestMatchProperty(property.Name);

                if (jsonProperty?.Writable == true)
                {
                    try
                    {
                        var value = property.Value.ToObject(jsonProperty.PropertyType, serializer);
                        jsonProperty.ValueProvider.SetValue(instance, value);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to set property {property.Name}: {ex.Message}");
                    }
                }
            }
        }
    }

    // Custom PopulateObject implementation
    public static class CustomJsonPopulate
    {
        public static void PopulateObject<T>(string json, T target, JsonSerializerSettings settings = null)
            where T : class
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (string.IsNullOrEmpty(json))
                return;

            // Create settings with our tracking converter
            settings = settings ?? new JsonSerializerSettings();
            var tracker = new DeserializationTracker();
            var trackingKey = tracker.CreateTrackingKey();

            // Add our converter if not already present
            var converterType = typeof(GenericTrackingConverter<>).MakeGenericType(typeof(T));
            var hasConverter = settings.Converters.Any(c => c.GetType() == converterType);

            if (!hasConverter)
            {
                settings.Converters.Add((JsonConverter)Activator.CreateInstance(converterType));
            }

            // Set tracking context
            settings.Context = new System.Runtime.Serialization.StreamingContext(
                System.Runtime.Serialization.StreamingContextStates.Other,
                trackingKey
            );

            try
            {
                // Step 1: Fully deserialize a new instance
                T newInstance = JsonConvert.DeserializeObject<T>(json, settings);

                if (newInstance == null)
                    return;

                // Step 2: Get the list of fields that were actually set
                var setFields = tracker.GetSetFields(trackingKey);

                // Step 3: Copy only the set fields from newInstance to target
                CopySetFields(newInstance, target, setFields, "");
            }
            finally
            {
                // Cleanup tracking data
                tracker.CleanupTracking(trackingKey);
            }
        }

        private static void CopySetFields<T>(T source, T target, HashSet<string> setFields, string basePath)
            where T : class
        {
            var type = typeof(T);
            var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            // Copy fields
            foreach (var field in type.GetFields(bindingFlags))
            {
                var fieldPath = string.IsNullOrEmpty(basePath)
                    ? field.Name
                    : $"{basePath}.{field.Name}";

                if (IsFieldSet(setFields, fieldPath))
                {
                    var value = field.GetValue(source);

                    // Handle nested objects
                    if (value != null && !field.FieldType.IsValueType &&
                        field.FieldType != typeof(string) && !field.FieldType.IsArray)
                    {
                        var targetValue = field.GetValue(target);
                        if (targetValue != null)
                        {
                            // Recursively copy nested object fields
                            CopyNestedObjectFields(value, targetValue, field.FieldType, setFields, fieldPath);
                            continue;
                        }
                    }

                    field.SetValue(target, value);
                }
            }

            // Copy properties
            foreach (var property in type.GetProperties(bindingFlags))
            {
                if (!property.CanRead || !property.CanWrite)
                    continue;

                var propPath = string.IsNullOrEmpty(basePath)
                    ? property.Name
                    : $"{basePath}.{property.Name}";

                if (IsFieldSet(setFields, propPath))
                {
                    var value = property.GetValue(source);

                    // Handle nested objects
                    if (value != null && !property.PropertyType.IsValueType &&
                        property.PropertyType != typeof(string) && !property.PropertyType.IsArray)
                    {
                        var targetValue = property.GetValue(target);
                        if (targetValue != null)
                        {
                            // Recursively copy nested object fields
                            CopyNestedObjectFields(value, targetValue, property.PropertyType, setFields, propPath);
                            continue;
                        }
                    }

                    property.SetValue(target, value);
                }
            }
        }

        private static void CopyNestedObjectFields(object source, object target, Type objectType,
            HashSet<string> setFields, string basePath)
        {
            var method = typeof(CustomJsonPopulate).GetMethod(nameof(CopySetFields),
                BindingFlags.NonPublic | BindingFlags.Static);
            var genericMethod = method.MakeGenericMethod(objectType);
            genericMethod.Invoke(null, new[] { source, target, setFields, basePath });
        }

        private static bool IsFieldSet(HashSet<string> setFields, string fieldPath)
        {
            // Check if this exact field was set, or any parent path
            return setFields.Contains(fieldPath) ||
                   setFields.Any(f => f.StartsWith(fieldPath + ".") || f.StartsWith(fieldPath + "["));
        }
    }
}
