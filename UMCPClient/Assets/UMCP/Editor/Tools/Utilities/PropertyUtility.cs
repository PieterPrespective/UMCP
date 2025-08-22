using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using System.Reflection;

namespace UMCP.Editor.Tools.Utilities
{
    /// <summary>
    /// Utility class for applying properties to Unity objects using reflection.
    /// Handles type conversion and property setting in a functional manner.
    /// </summary>
    public static class PropertyUtility
    {
        /// <summary>
        /// Generic helper to set properties on any UnityEngine.Object using reflection.
        /// </summary>
        /// <param name="target">The target object to modify</param>
        /// <param name="properties">JObject containing property-value pairs</param>
        /// <returns>True if any properties were modified, false otherwise</returns>
        public static bool ApplyObjectProperties(UnityEngine.Object target, JObject properties)
        {
            if (target == null || properties == null) return false;
            
            bool modified = false;
            Type type = target.GetType();

            foreach (var prop in properties.Properties())
            {
                string propName = prop.Name;
                JToken propValue = prop.Value;
                if (SetPropertyOrField(target, propName, propValue, type))
                {
                    modified = true;
                }
            }
            
            return modified;
        }

        /// <summary>
        /// Helper to set a property or field via reflection, handling basic types and Unity objects.
        /// </summary>
        /// <param name="target">The target object</param>
        /// <param name="memberName">The property or field name</param>
        /// <param name="value">The value to set</param>
        /// <param name="type">The target object type (optional, will be inferred if not provided)</param>
        /// <returns>True if the property/field was successfully set, false otherwise</returns>
        public static bool SetPropertyOrField(object target, string memberName, JToken value, Type type = null)
        {
            type = type ?? target.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

            try
            {
                PropertyInfo propInfo = type.GetProperty(memberName, flags);
                if (propInfo != null && propInfo.CanWrite)
                {
                    object convertedValue = TypeConversionUtility.ConvertJTokenToType(value, propInfo.PropertyType);
                    if (convertedValue != null && !object.Equals(propInfo.GetValue(target), convertedValue))
                    {
                        propInfo.SetValue(target, convertedValue);
                        return true;
                    }
                }
                else
                {
                    FieldInfo fieldInfo = type.GetField(memberName, flags);
                    if (fieldInfo != null)
                    {
                        object convertedValue = TypeConversionUtility.ConvertJTokenToType(value, fieldInfo.FieldType);
                        if (convertedValue != null && !object.Equals(fieldInfo.GetValue(target), convertedValue))
                        {
                            fieldInfo.SetValue(target, convertedValue);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PropertyUtility.SetPropertyOrField] Failed to set '{memberName}' on {type.Name}: {ex.Message}");
            }
            
            return false;
        }
    }
}