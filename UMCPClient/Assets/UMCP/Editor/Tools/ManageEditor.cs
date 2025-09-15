using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Generic;
using UMCP.Editor.Helpers;
using UMCP.Editor.Tools.Utilities;
using System.Threading.Tasks;

namespace UMCP.Editor.Tools
{
    /// <summary>
    /// Handles operations related to controlling and querying the Unity Editor state,
    /// including managing Tags and Layers. Refactored to use functional programming utilities.
    /// </summary>
    public static class ManageEditor
    {
        /// <summary>
        /// Main handler for editor management actions using functional utilities.
        /// </summary>
        public static async Task<object> HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString().ToLower();
            
            if (string.IsNullOrEmpty(action))
            {
                return Response.Error("Action parameter is required.");
            }

            // Validate action using utility
            if (!ManageEditorUtility.IsValidAction(action))
            {
                return Response.Error($"Unknown action: '{action}'. Supported actions: {string.Join(", ", ManageEditorUtility.ValidActions)}.");
            }

            // Parameters for specific actions
            string tagName = @params["tagName"]?.ToString();
            string layerName = @params["layerName"]?.ToString();
            string toolName = @params["toolName"]?.ToString();

            // Route action to appropriate utility function
            return await Task.FromResult( action switch
            {
                // Play Mode Control
                "play" => ManageEditorUtility.PlayMode(),
                "pause" => ManageEditorUtility.PauseMode(),
                "stop" => ManageEditorUtility.StopMode(),

                // Editor State/Info
                "get_state" => ManageEditorUtility.GetEditorState(),
                "get_windows" => ManageEditorUtility.GetEditorWindows(),
                "get_active_tool" => ManageEditorUtility.GetActiveTool(),
                "get_selection" => ManageEditorUtility.GetSelection(),
                "set_active_tool" => ValidateAndExecute(() => ManageEditorUtility.SetActiveTool(toolName), 
                    string.IsNullOrEmpty(toolName) ? "'toolName' parameter required for set_active_tool." : null),

                // Tag Management
                "add_tag" => ValidateAndExecute(() => ManageEditorUtility.AddTag(tagName), 
                    string.IsNullOrEmpty(tagName) ? "'tagName' parameter required for add_tag." : null),
                "remove_tag" => ValidateAndExecute(() => ManageEditorUtility.RemoveTag(tagName), 
                    string.IsNullOrEmpty(tagName) ? "'tagName' parameter required for remove_tag." : null),
                "get_tags" => ManageEditorUtility.GetTags(),

                // Layer Management
                "add_layer" => ValidateAndExecute(() => ManageEditorUtility.AddLayer(layerName), 
                    string.IsNullOrEmpty(layerName) ? "'layerName' parameter required for add_layer." : null),
                "remove_layer" => ValidateAndExecute(() => ManageEditorUtility.RemoveLayer(layerName), 
                    string.IsNullOrEmpty(layerName) ? "'layerName' parameter required for remove_layer." : null),
                "get_layers" => ManageEditorUtility.GetLayers(),

                // Default case (should never hit due to validation above)
                _ => Response.Error($"Unknown action: '{action}'.")
            });
        }

        /// <summary>
        /// Helper method to validate parameters before executing utility functions.
        /// </summary>
        /// <param name="action">The action to execute if validation passes</param>
        /// <param name="validationError">The validation error message, or null if valid</param>
        /// <returns>The result of the action or validation error</returns>
        private static object ValidateAndExecute(Func<object> action, string validationError)
        {
            if (!string.IsNullOrEmpty(validationError))
            {
                return Response.Error(validationError);
            }
            
            return action();
        }
    }
}