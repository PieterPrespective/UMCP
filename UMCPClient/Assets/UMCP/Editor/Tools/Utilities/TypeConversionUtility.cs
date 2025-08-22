using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;

namespace UMCP.Editor.Tools.Utilities
{
    /// <summary>
    /// Utility class for converting JToken values to Unity types.
    /// Handles common Unity types and primitives in a functional manner.
    /// </summary>
    public static class TypeConversionUtility
    {
        /// <summary>
        /// Simple JToken to Type conversion for common Unity types and primitives.
        /// </summary>
        /// <param name="token">The JToken to convert</param>
        /// <param name="targetType">The target type to convert to</param>
        /// <returns>Converted object or null if conversion failed</returns>
        public static object ConvertJTokenToType(JToken token, Type targetType)
        {
            try
            {
                if (token == null || token.Type == JTokenType.Null) return null;

                // Handle primitive types
                if (targetType == typeof(string)) return token.ToObject<string>();
                if (targetType == typeof(int)) return token.ToObject<int>();
                if (targetType == typeof(float)) return token.ToObject<float>();
                if (targetType == typeof(bool)) return token.ToObject<bool>();
                if (targetType == typeof(double)) return token.ToObject<double>();
                if (targetType == typeof(long)) return token.ToObject<long>();

                // Handle Unity vector types
                if (targetType == typeof(Vector2)) return ConvertToVector2(token);
                if (targetType == typeof(Vector3)) return ConvertToVector3(token);
                if (targetType == typeof(Vector4)) return ConvertToVector4(token);
                if (targetType == typeof(Quaternion)) return ConvertToQuaternion(token);
                if (targetType == typeof(Color)) return ConvertToColor(token);

                // Handle enums
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, token.ToString(), true);

                // Handle Unity Objects (Materials, Textures, etc.) by path
                if (typeof(UnityEngine.Object).IsAssignableFrom(targetType) && token.Type == JTokenType.String)
                {
                    return LoadUnityAsset(token.ToString(), targetType);
                }

                // Fallback: Try direct conversion
                return token.ToObject(targetType);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TypeConversionUtility.ConvertJTokenToType] Could not convert JToken '{token}' (type {token.Type}) to type '{targetType.Name}': {ex.Message}");
                return null;
            }
        }

        private static Vector2 ConvertToVector2(JToken token)
        {
            if (token is JArray arrV2 && arrV2.Count == 2)
            {
                return new Vector2(arrV2[0].ToObject<float>(), arrV2[1].ToObject<float>());
            }
            throw new ArgumentException("Vector2 requires array with 2 elements");
        }

        private static Vector3 ConvertToVector3(JToken token)
        {
            if (token is JArray arrV3 && arrV3.Count == 3)
            {
                return new Vector3(arrV3[0].ToObject<float>(), arrV3[1].ToObject<float>(), arrV3[2].ToObject<float>());
            }
            throw new ArgumentException("Vector3 requires array with 3 elements");
        }

        private static Vector4 ConvertToVector4(JToken token)
        {
            if (token is JArray arrV4 && arrV4.Count == 4)
            {
                return new Vector4(arrV4[0].ToObject<float>(), arrV4[1].ToObject<float>(), arrV4[2].ToObject<float>(), arrV4[3].ToObject<float>());
            }
            throw new ArgumentException("Vector4 requires array with 4 elements");
        }

        private static Quaternion ConvertToQuaternion(JToken token)
        {
            if (token is JArray arrQ && arrQ.Count == 4)
            {
                return new Quaternion(arrQ[0].ToObject<float>(), arrQ[1].ToObject<float>(), arrQ[2].ToObject<float>(), arrQ[3].ToObject<float>());
            }
            throw new ArgumentException("Quaternion requires array with 4 elements");
        }

        private static Color ConvertToColor(JToken token)
        {
            if (token is JArray arrC && arrC.Count >= 3)
            {
                return new Color(
                    arrC[0].ToObject<float>(), 
                    arrC[1].ToObject<float>(), 
                    arrC[2].ToObject<float>(), 
                    arrC.Count > 3 ? arrC[3].ToObject<float>() : 1.0f
                );
            }
            throw new ArgumentException("Color requires array with 3 or 4 elements");
        }

        private static UnityEngine.Object LoadUnityAsset(string assetPath, Type targetType)
        {
            string sanitizedPath = ManageAssetUtility.SanitizeAssetPath(assetPath);
            UnityEngine.Object loadedAsset = AssetDatabase.LoadAssetAtPath(sanitizedPath, targetType);
            if (loadedAsset == null)
            {
                Debug.LogWarning($"[TypeConversionUtility.LoadUnityAsset] Could not load asset of type {targetType.Name} from path: {sanitizedPath}");
            }
            return loadedAsset;
        }
    }
}