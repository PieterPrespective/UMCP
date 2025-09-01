using UnityEngine;

namespace UMCP.Editor.SerialTracker
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using UnityEngine;

    // Data class to hold Transform local properties
    [Serializable]
    public class TransformLocalData
    {
        public Vector3 localPosition { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 localScale { get; set; }

        // Constructor to create from Transform
        public TransformLocalData() { }

        public TransformLocalData(Transform transform)
        {
            localPosition = transform.localPosition;
            localRotation = transform.localRotation;
            localScale = transform.localScale;
        }

        // Apply data back to Transform
        public void ApplyToTransform(Transform transform)
        {
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;
        }
    }

    // Custom Vector3 converter for clean JSON
    public class Vector3Converter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WriteEndObject();
        }

        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var jo = JObject.Load(reader);
            return new Vector3(
                jo["x"]?.Value<float>() ?? 0f,
                jo["y"]?.Value<float>() ?? 0f,
                jo["z"]?.Value<float>() ?? 0f
            );
        }
    }

    // Custom Quaternion converter for clean JSON
    public class QuaternionConverter : JsonConverter<Quaternion>
    {
        public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WritePropertyName("w");
            writer.WriteValue(value.w);
            writer.WriteEndObject();
        }

        public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var jo = JObject.Load(reader);
            return new Quaternion(
                jo["x"]?.Value<float>() ?? 0f,
                jo["y"]?.Value<float>() ?? 0f,
                jo["z"]?.Value<float>() ?? 0f,
                jo["w"]?.Value<float>() ?? 1f
            );
        }
    }

    // Specialized converter for TransformLocalData with tracking
    public class TransformLocalDataConverter : TrackingJsonConverter<TransformLocalData>
    {
        private readonly Vector3Converter _vector3Converter = new Vector3Converter();
        private readonly QuaternionConverter _quaternionConverter = new QuaternionConverter();

        protected override TransformLocalData CreateInstance()
        {
            return new TransformLocalData
            {
                localPosition = Vector3.zero,
                localRotation = Quaternion.identity,
                localScale = Vector3.one
            };
        }

        protected override void PopulateInstance(TransformLocalData instance, JObject jsonObject,
            JsonSerializer serializer, string trackingKey)
        {
            // Only deserialize fields that are present in the JSON
            if (jsonObject["localPosition"] != null)
            {
                instance.localPosition = jsonObject["localPosition"].ToObject<Vector3>(
                    JsonSerializer.Create(new JsonSerializerSettings
                    {
                        Converters = { _vector3Converter }
                    })
                );
            }

            if (jsonObject["localRotation"] != null)
            {
                instance.localRotation = jsonObject["localRotation"].ToObject<Quaternion>(
                    JsonSerializer.Create(new JsonSerializerSettings
                    {
                        Converters = { _quaternionConverter }
                    })
                );
            }

            if (jsonObject["localScale"] != null)
            {
                instance.localScale = jsonObject["localScale"].ToObject<Vector3>(
                    JsonSerializer.Create(new JsonSerializerSettings
                    {
                        Converters = { _vector3Converter }
                    })
                );
            }

            // Log which fields were actually set
            var setFields = GetSetFields(trackingKey);
            Debug.Log($"Transform fields set: {string.Join(", ", setFields)}");
        }

        public override void WriteJson(JsonWriter writer, TransformLocalData value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("localPosition");
            _vector3Converter.WriteJson(writer, value.localPosition, serializer);

            writer.WritePropertyName("localRotation");
            _quaternionConverter.WriteJson(writer, value.localRotation, serializer);

            writer.WritePropertyName("localScale");
            _vector3Converter.WriteJson(writer, value.localScale, serializer);

            writer.WriteEndObject();
        }
    }
}
