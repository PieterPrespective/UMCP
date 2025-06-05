using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using UMCP.Editor.Helpers;

namespace UMCP.Editor.Tools
{
    /// <summary>
    /// Marks the start of a new development step by creating a log entry in the Unity Debug Console.
    /// This allows for tracking which log messages are relevant to specific development steps.
    /// </summary>
    public static class MarkStartOfNewStep
    {
        // Static marker format for easy identification
        private const string MARKER_PREFIX = "[UMCP_STEP_START]";
        private const string MARKER_SUFFIX = "[/UMCP_STEP_START]";
        
        public static object HandleCommand(JObject @params)
        {
            try
            {
                // Extract the step name parameter
                string stepName = @params["stepName"]?.ToString();
                
                if (string.IsNullOrEmpty(stepName))
                {
                    return Response.Error("Step name cannot be empty. Please provide a valid step name.");
                }
                
                // Create a timestamp for the marker
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                
                // Create the marker message with clear delimiters
                string markerMessage = $"{MARKER_PREFIX} Step: '{stepName}' | Started at: {timestamp} {MARKER_SUFFIX}";
                
                // Log to Unity Console
                Debug.Log(markerMessage);
                
                // Also log a more human-readable message
                Debug.Log($"=== Development Step Started: {stepName} ===");
                
                return Response.Success($"Successfully marked start of step '{stepName}' at {timestamp}", new
                {
                    stepName = stepName,
                    timestamp = timestamp,
                    markerMessage = markerMessage
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[MarkStartOfNewStep] Error: {e.Message}");
                return Response.Error($"Failed to mark start of step: {e.Message}");
            }
        }
        
        /// <summary>
        /// Gets the marker prefix used to identify step start markers in logs.
        /// Used by RequestStepLogs to find the start of a step.
        /// </summary>
        public static string GetMarkerPrefix()
        {
            return MARKER_PREFIX;
        }
        
        /// <summary>
        /// Checks if a log message is a step start marker for the given step name.
        /// </summary>
        public static bool IsStepStartMarker(string message, string stepName)
        {
            if (string.IsNullOrEmpty(message))
                return false;
                
            return message.Contains(MARKER_PREFIX) && 
                   message.Contains($"Step: '{stepName}'") && 
                   message.Contains(MARKER_SUFFIX);
        }
        
        /// <summary>
        /// Extracts the step name from a marker message.
        /// </summary>
        public static string ExtractStepName(string markerMessage)
        {
            try
            {
                int startIndex = markerMessage.IndexOf("Step: '") + 7;
                int endIndex = markerMessage.IndexOf("'", startIndex);
                
                if (startIndex > 6 && endIndex > startIndex)
                {
                    return markerMessage.Substring(startIndex, endIndex - startIndex);
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            
            return null;
        }
    }
}
