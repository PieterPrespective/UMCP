using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UMCP.Editor.Helpers;

namespace UMCP.Editor.Tools
{
    /// <summary>
    /// Retrieves log messages for a specific development step, going back to the mark_start_of_new_step invocation.
    /// </summary>
    public static class RequestStepLogs
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                // Extract parameters
                string stepName = @params["stepName"]?.ToString();
                bool includeStacktrace = @params["includeStacktrace"]?.ToObject<bool?>() ?? true;
                string format = (@params["format"]?.ToString() ?? "detailed").ToLower();
                
                if (string.IsNullOrEmpty(stepName))
                {
                    return Response.Error("Step name cannot be empty. Please provide a valid step name.");
                }
                
                // Get all console entries using ReadConsole functionality
                var allLogsResult = ReadConsole.HandleCommand(new JObject
                {
                    ["action"] = "get",
                    ["types"] = new JArray("error", "warning", "log"),
                    ["format"] = "detailed",
                    ["includeStacktrace"] = includeStacktrace
                });
                
                // Extract the logs data
                dynamic logsResponse = allLogsResult;
                if (logsResponse.status != "success" || logsResponse.data == null)
                {
                    return Response.Error("Failed to retrieve console logs.");
                }
                
                var allLogs = logsResponse.data as IEnumerable<dynamic>;
                if (allLogs == null)
                {
                    return Response.Error("Failed to parse console logs.");
                }
                
                // Find the step start marker
                List<object> stepLogs = new List<object>();
                bool foundStepStart = false;
                
                // Process logs in reverse order (newest first) to find the most recent step start
                var logsArray = allLogs.ToArray();
                for (int i = logsArray.Length - 1; i >= 0; i--)
                {
                    var log = logsArray[i];
                    string message = log.message?.ToString() ?? "";
                    
                    // Check if this is the step start marker
                    if (MarkStartOfNewStep.IsStepStartMarker(message, stepName))
                    {
                        foundStepStart = true;
                        // Include the marker itself
                        stepLogs.Insert(0, FormatLogEntry(log, format));
                        
                        // Now collect all logs after this marker (in forward order)
                        for (int j = i + 1; j < logsArray.Length; j++)
                        {
                            stepLogs.Add(FormatLogEntry(logsArray[j], format));
                        }
                        break;
                    }
                }
                
                if (!foundStepStart)
                {
                    // Try to find any step marker that contains the step name (partial match)
                    for (int i = logsArray.Length - 1; i >= 0; i--)
                    {
                        var log = logsArray[i];
                        string message = log.message?.ToString() ?? "";
                        
                        if (message.Contains(MarkStartOfNewStep.GetMarkerPrefix()) && 
                            message.ToLower().Contains(stepName.ToLower()))
                        {
                            foundStepStart = true;
                            string actualStepName = MarkStartOfNewStep.ExtractStepName(message) ?? stepName;
                            
                            // Include the marker itself
                            stepLogs.Insert(0, FormatLogEntry(log, format));
                            
                            // Collect all logs after this marker
                            for (int j = i + 1; j < logsArray.Length; j++)
                            {
                                stepLogs.Add(FormatLogEntry(logsArray[j], format));
                            }
                            
                            return Response.Success(
                                $"Found logs for similar step '{actualStepName}' (searched for '{stepName}'). " +
                                $"Retrieved {stepLogs.Count} log entries.",
                                stepLogs
                            );
                        }
                    }
                }
                
                if (!foundStepStart)
                {
                    return Response.Error($"No start marker found for step '{stepName}'. " +
                        "Make sure to call 'mark_start_of_new_step' before requesting step logs.");
                }
                
                return Response.Success($"Retrieved {stepLogs.Count} log entries for step '{stepName}'.", stepLogs);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RequestStepLogs] Error: {e.Message}");
                return Response.Error($"Failed to retrieve step logs: {e.Message}");
            }
        }
        
        /// <summary>
        /// Formats a log entry based on the requested format.
        /// </summary>
        private static object FormatLogEntry(dynamic log, string format)
        {
            if (format == "plain")
            {
                return log.message?.ToString() ?? "";
            }
            
            // Default to detailed format
            return log;
        }
    }
}
