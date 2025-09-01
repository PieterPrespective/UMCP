using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using UMCP.Editor.Helpers;
using UnityEditor;
using UnityEngine;

namespace UMCP.Editor
{
    /// <summary>
    /// Generic utility for processing Unity Objects (GameObjects, Components, Assets
    /// </summary>
    public static class UnityObjectUtility 
    {

        public static object ModifyAsset(string path, JObject properties)
        {
            if (string.IsNullOrEmpty(path)) return Response.Error("'path' is required for modify.");
            if (properties == null || !properties.HasValues) return Response.Error("'properties' are required for modify.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath)) return Response.Error($"Asset not found at path: {fullPath}");

            throw new NotImplementedException();
        }

        /// <summary>
        /// Ensures the asset path starts with "Assets/".
        /// </summary>
        private static string SanitizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = path.Replace('\\', '/'); // Normalize separators
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets/" + path.TrimStart('/');
            }
            return path;
        }

        /// <summary>
        /// Checks if an asset exists at the given path (file or folder).
        /// </summary>
        private static bool AssetExists(string sanitizedPath)
        {
            // AssetDatabase APIs are generally preferred over raw File/Directory checks for assets.
            // Check if it's a known asset GUID.
            //if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sanitizedPath)) && AssetDatabase.GetAss)
            //{
            //    return true;
            //}
            // AssetPathToGUID might not work for newly created folders not yet refreshed.
            // Check directory explicitly for folders.
            if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                // Check if it's considered a *valid* folder by Unity
                return AssetDatabase.IsValidFolder(sanitizedPath);
            }
            // Check file existence for non-folder assets.
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                return true; // Assume if file exists, it's an asset or will be imported
            }

            return false;
            // Alternative: return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sanitizedPath));
        }



        /// <summary>
        /// Creates a serializable representation of a Component.
        /// TODO: Add property serialization.
        /// </summary>
        internal static object GetComponentData(Component c, EnrichedConversionContext enrichedConversionContext = null)
        {
            if (c == null) return null;

            var data = new Dictionary<string, object> {
                 { "typeName", c.GetType().FullName },
                 { "instanceID", c.GetInstanceID() }
             };

            if (UnityObjectUtility.TrySerializeObject(c, c.GetType(), out string serialResult, out Exception serializationException, enrichedConversionContext))
            {
                data["properties"] = JObject.Parse(serialResult).ToObject<object>();
            }
            else if (serializationException != null)
            {
                data["properties"] = $"Exporting type '{c.GetType().FullName}' resulted in an error: " + serializationException.Message;
            }

            return data;
        }



        public static bool TryPopulateObject(ref UnityEngine.Object _existingInstance, string _serialData, out Exception _populationError, EnrichedConversionContext conversionContext)
        {
            Debug.Log($"[TryPopulateObject] Attempting to populate instance of type {_existingInstance.GetType().FullName} with data: {_serialData}");

            _populationError = null;
            System.Type _type = _existingInstance.GetType();

            if (!CustomUMCPTypeFormatterService.TryFindBestConverter(_type, out IEnrichedJsonConverter _convertor, out int _matchDistance))
            {
                _populationError = new Exception($"No converter found for type {_type.Name}, cannot populate instance.");
                return false;
            }

            var serializer = JsonSerializer.Create(new JsonSerializerSettings()
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                Context = new System.Runtime.Serialization.StreamingContext(System.Runtime.Serialization.StreamingContextStates.All, conversionContext),
                Converters = CustomUMCPTypeFormatterService.JSONConverters,
            });

            JsonReader jsonReader = new JsonTextReader(new System.IO.StringReader(_serialData));
            try
            {
                ((Newtonsoft.Json.JsonConverter)_convertor).ReadJson(jsonReader, _convertor.TargetType, _existingInstance, serializer);
                Debug.Log($"[TryPopulateObject] Successfully populated instance of type {_existingInstance.GetType().FullName} (ID {_existingInstance.GetInstanceID()})");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[TryPopulateObject] Error populating instance of type {_type.FullName}: {e.Message}\n{e.StackTrace}");
                _populationError = e;
                return false;
            }
        }


        /// <summary>
        /// Attempt to serialize the UnityEngine Object
        /// </summary>
        /// <param name="_object">reference to the object to serialize</param>
        /// <param name="_objectType">Type of the Object we're looking to serialize</param>
        /// <param name="_result">result if succesfull</param>
        /// <param name="_serializationException">Exception if not succesful</param>
        /// <returns>whether serialization succeeded</returns>
        public static bool TrySerializeObject(UnityEngine.Object _object, System.Type _objectType, out string _result, out System.Exception _serializationException, EnrichedConversionContext conversionContext)
        {
            _serializationException = null;
            _result = null;
            //Only attempt serialization if we can find a proper converter
            if (CustomUMCPTypeFormatterService.TryFindBestConverter(_objectType, out _, out int distance))
            {
                try
                {
                    if (distance > 0)
                    {
                        Debug.LogWarning($"[GetComponentData] Using non-exact converter for type '{_objectType.FullName}' with distance {distance}. This may lead to incomplete or incorrect serialization.");
                    }

                    if (CustomUMCPTypeFormatterService.TrySerializeDirect(_object, out _result, out Exception _serialFail, conversionContext))
                    {
                        return true;
                    }
                    else
                    {
                        _serializationException = new Exception($"Exporting type '{_objectType.FullName}' resulted in an error: " + _serialFail.Message, _serialFail);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _serializationException = new Exception($"Exporting type properties '{_objectType.FullName}' resulted in an error: " + ex.Message, ex);
                    return false;
                }
            }
            else
            {
                _serializationException = new Exception($"Could not export properties on type '{_objectType.FullName}', no custom converters are available");
                return false;
            }
        }
    }
}
