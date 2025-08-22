using System;
using UnityEngine;

namespace UMCP.Editor.Tools.Utilities
{
    /// <summary>
    /// Utility class for reflection-based type operations.
    /// Provides methods to find types by name in a functional manner.
    /// </summary>
    public static class ReflectionUtility
    {
        /// <summary>
        /// Helper to find a Type by name, searching relevant assemblies.
        /// Needed for creating ScriptableObjects or finding component types by name.
        /// </summary>
        /// <param name="typeName">The name of the type to find</param>
        /// <returns>The Type if found, null otherwise</returns>
        public static Type FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            // Try direct lookup first (common Unity types often don't need assembly qualified name)
            var type = TryDirectTypeLookup(typeName);
            if (type != null) return type;

            // If not found, search loaded assemblies (slower but more robust for user scripts)
            return SearchAssembliesForType(typeName);
        }

        private static Type TryDirectTypeLookup(string typeName)
        {
            return Type.GetType(typeName) ??
                   Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule") ??
                   Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI") ??
                   Type.GetType($"UnityEditor.{typeName}, UnityEditor.CoreModule");
        }

        private static Type SearchAssembliesForType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Look for non-namespaced first
                Type type = assembly.GetType(typeName, false, true); // throwOnError=false, ignoreCase=true
                if (type != null) return type;

                // Check common namespaces if simple name given
                type = assembly.GetType("UnityEngine." + typeName, false, true);
                if (type != null) return type;
                
                type = assembly.GetType("UnityEditor." + typeName, false, true);
                if (type != null) return type;
            }

            Debug.LogWarning($"[ReflectionUtility.FindType] Type '{typeName}' not found in any loaded assembly.");
            return null;
        }
    }
}