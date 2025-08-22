using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UMCP.Editor.Helpers;

namespace UMCP.Editor.Tools.Utilities
{
    /// <summary>
    /// Static utility class containing Unity Editor management processing logic.
    /// Separates business logic from data structures following functional programming principles.
    /// </summary>
    public static class ManageEditorUtility
    {
        #region Constants and Validation

        /// <summary>
        /// List of valid actions supported by the ManageEditor tool.
        /// Excludes create_folder and refresh_assets as per assignment requirements.
        /// </summary>
        public static readonly List<string> ValidActions = new List<string>
        {
            "play", "pause", "stop", "get_state", "get_windows", "get_active_tool", 
            "get_selection", "set_active_tool", "add_tag", "remove_tag", "get_tags", 
            "add_layer", "remove_layer", "get_layers"
        };

        /// <summary>
        /// Constants for layer management.
        /// </summary>
        private const int FirstUserLayerIndex = 8;
        private const int TotalLayerCount = 32;

        /// <summary>
        /// Validates if an action is supported.
        /// </summary>
        /// <param name="action">The action to validate</param>
        /// <returns>True if action is valid, false otherwise</returns>
        public static bool IsValidAction(string action)
        {
            return !string.IsNullOrEmpty(action) && ValidActions.Contains(action.ToLower());
        }

        #endregion

        #region Play Mode Control

        /// <summary>
        /// Enters Unity play mode.
        /// </summary>
        /// <returns>Response object with operation result</returns>
        public static object PlayMode()
        {
            try
            {
                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = true;
                    return Response.Success("Entered play mode.");
                }
                return Response.Success("Already in play mode.");
            }
            catch (Exception e)
            {
                return Response.Error($"Error entering play mode: {e.Message}");
            }
        }

        /// <summary>
        /// Pauses or resumes Unity play mode.
        /// </summary>
        /// <returns>Response object with operation result</returns>
        public static object PauseMode()
        {
            try
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPaused = !EditorApplication.isPaused;
                    return Response.Success(EditorApplication.isPaused ? "Game paused." : "Game resumed.");
                }
                return Response.Error("Cannot pause/resume: Not in play mode.");
            }
            catch (Exception e)
            {
                return Response.Error($"Error pausing/resuming game: {e.Message}");
            }
        }

        /// <summary>
        /// Exits Unity play mode.
        /// </summary>
        /// <returns>Response object with operation result</returns>
        public static object StopMode()
        {
            try
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                    return Response.Success("Exited play mode.");
                }
                return Response.Success("Already stopped (not in play mode).");
            }
            catch (Exception e)
            {
                return Response.Error($"Error stopping play mode: {e.Message}");
            }
        }

        #endregion

        #region Editor State Information

        /// <summary>
        /// Retrieves current Unity Editor state information.
        /// </summary>
        /// <returns>Response object with editor state data</returns>
        public static object GetEditorState()
        {
            try
            {
                var state = new EditorStateData
                {
                    IsPlaying = EditorApplication.isPlaying,
                    IsPaused = EditorApplication.isPaused,
                    IsCompiling = EditorApplication.isCompiling,
                    IsUpdating = EditorApplication.isUpdating,
                    ApplicationPath = EditorApplication.applicationPath,
                    ApplicationContentsPath = EditorApplication.applicationContentsPath,
                    TimeSinceStartup = EditorApplication.timeSinceStartup
                };

                return Response.Success("Retrieved editor state.", state);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting editor state: {e.Message}");
            }
        }

        /// <summary>
        /// Retrieves list of open Unity Editor windows.
        /// </summary>
        /// <returns>Response object with window list</returns>
        public static object GetEditorWindows()
        {
            try
            {
                var openWindows = new List<EditorWindowData>();
                EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();

                foreach (EditorWindow window in allWindows)
                {
                    if (window == null) continue;
                    
                    try
                    {
                        var windowData = new EditorWindowData
                        {
                            Title = window.titleContent.text,
                            TypeName = window.GetType().FullName,
                            IsFocused = EditorWindow.focusedWindow == window,
                            Position = new WindowPositionData
                            {
                                X = window.position.x,
                                Y = window.position.y,
                                Width = window.position.width,
                                Height = window.position.height
                            },
                            InstanceID = window.GetInstanceID()
                        };
                        
                        openWindows.Add(windowData);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Could not get info for window {window.GetType().Name}: {ex.Message}");
                    }
                }

                return Response.Success("Retrieved list of open editor windows.", openWindows);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting editor windows: {e.Message}");
            }
        }

        /// <summary>
        /// Retrieves information about the currently active Unity tool.
        /// </summary>
        /// <returns>Response object with active tool data</returns>
        public static object GetActiveTool()
        {
            try
            {
                Tool currentTool = UnityEditor.Tools.current;
                string toolName = currentTool.ToString();
                bool customToolActive = UnityEditor.Tools.current == Tool.Custom;
                string activeToolName = customToolActive ? GetActiveCustomToolName() : toolName;
                
                var toolInfo = new ActiveToolData
                {
                    ActiveTool = activeToolName,
                    IsCustom = customToolActive,
                    PivotMode = UnityEditor.Tools.pivotMode.ToString(),
                    PivotRotation = UnityEditor.Tools.pivotRotation.ToString(),
                    HandleRotation = UnityEditor.Tools.handleRotation.eulerAngles,
                    HandlePosition = UnityEditor.Tools.handlePosition
                };

                return Response.Success("Retrieved active tool information.", toolInfo);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting active tool: {e.Message}");
            }
        }

        /// <summary>
        /// Sets the active Unity tool.
        /// </summary>
        /// <param name="toolName">The name of the tool to activate</param>
        /// <returns>Response object with operation result</returns>
        public static object SetActiveTool(string toolName)
        {
            try
            {
                if (Enum.TryParse<Tool>(toolName, true, out Tool targetTool))
                {
                    if (targetTool != Tool.None && targetTool <= Tool.Custom)
                    {
                        UnityEditor.Tools.current = targetTool;
                        return Response.Success($"Set active tool to '{targetTool}'.");
                    }
                    else
                    { 
                        return Response.Error($"Cannot directly set tool to '{toolName}'. It might be None, Custom, or invalid.");
                    }
                }
                else
                {
                    return Response.Error($"Could not parse '{toolName}' as a standard Unity Tool (View, Move, Rotate, Scale, Rect, Transform, Custom).");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error setting active tool: {e.Message}");
            }
        }

        /// <summary>
        /// Retrieves current Unity Editor selection information.
        /// </summary>
        /// <returns>Response object with selection data</returns>
        public static object GetSelection()
        {
            try
            {
                var selectionInfo = new SelectionData
                {
                    ActiveObject = Selection.activeObject?.name,
                    ActiveGameObject = Selection.activeGameObject?.name,
                    ActiveTransform = Selection.activeTransform?.name,
                    ActiveInstanceID = Selection.activeInstanceID,
                    Count = Selection.count,
                    Objects = Selection.objects.Select(obj => new SelectionObjectData 
                    { 
                        Name = obj?.name, 
                        Type = obj?.GetType().FullName, 
                        InstanceID = obj?.GetInstanceID() ?? 0 
                    }).ToList(),
                    GameObjects = Selection.gameObjects.Select(go => new SelectionGameObjectData 
                    { 
                        Name = go?.name, 
                        InstanceID = go?.GetInstanceID() ?? 0 
                    }).ToList(),
                    AssetGUIDs = Selection.assetGUIDs.ToList()
                };

                return Response.Success("Retrieved current selection details.", selectionInfo);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting selection: {e.Message}");
            }
        }

        #endregion

        #region Tag Management

        /// <summary>
        /// Adds a new tag to Unity's tag system.
        /// </summary>
        /// <param name="tagName">The name of the tag to add</param>
        /// <returns>Response object with operation result</returns>
        public static object AddTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return Response.Error("Tag name cannot be empty or whitespace.");

            if (InternalEditorUtility.tags.Contains(tagName))
            {
                return Response.Error($"Tag '{tagName}' already exists.");
            }

            try
            {
                InternalEditorUtility.AddTag(tagName);
                AssetDatabase.SaveAssets();
                return Response.Success($"Tag '{tagName}' added successfully.");
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to add tag '{tagName}': {e.Message}");
            }
        }

        /// <summary>
        /// Removes a tag from Unity's tag system.
        /// </summary>
        /// <param name="tagName">The name of the tag to remove</param>
        /// <returns>Response object with operation result</returns>
        public static object RemoveTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return Response.Error("Tag name cannot be empty or whitespace.");
            
            if (tagName.Equals("Untagged", StringComparison.OrdinalIgnoreCase))
                return Response.Error("Cannot remove the built-in 'Untagged' tag.");

            if (!InternalEditorUtility.tags.Contains(tagName))
            {
                return Response.Error($"Tag '{tagName}' does not exist.");
            }

            try
            {
                InternalEditorUtility.RemoveTag(tagName);
                AssetDatabase.SaveAssets();
                return Response.Success($"Tag '{tagName}' removed successfully.");
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to remove tag '{tagName}': {e.Message}");
            }
        }

        /// <summary>
        /// Retrieves all current tags in Unity's tag system.
        /// </summary>
        /// <returns>Response object with tag list</returns>
        public static object GetTags()
        {
            try
            {
                string[] tags = InternalEditorUtility.tags;
                return Response.Success("Retrieved current tags.", tags);
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to retrieve tags: {e.Message}");
            }
        }

        #endregion

        #region Layer Management

        /// <summary>
        /// Adds a new layer to Unity's layer system.
        /// </summary>
        /// <param name="layerName">The name of the layer to add</param>
        /// <returns>Response object with operation result</returns>
        public static object AddLayer(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return Response.Error("Layer name cannot be empty or whitespace.");

            SerializedObject tagManager = GetTagManager();
            if (tagManager == null) 
                return Response.Error("Could not access TagManager asset.");

            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (layersProp == null || !layersProp.isArray)
                return Response.Error("Could not find 'layers' property in TagManager.");

            // Check if layer name already exists
            for (int i = 0; i < TotalLayerCount; i++)
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                if (layerSP != null && layerName.Equals(layerSP.stringValue, StringComparison.OrdinalIgnoreCase))
                {
                    return Response.Error($"Layer '{layerName}' already exists at index {i}.");
                }
            }

            // Find the first empty user layer slot
            int firstEmptyUserLayer = FindFirstEmptyLayerSlot(layersProp);
            if (firstEmptyUserLayer == -1)
            {
                return Response.Error("No empty User Layer slots available (8-31 are full).");
            }

            try
            {
                SerializedProperty targetLayerSP = layersProp.GetArrayElementAtIndex(firstEmptyUserLayer);
                targetLayerSP.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                return Response.Success($"Layer '{layerName}' added successfully to slot {firstEmptyUserLayer}.");
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to add layer '{layerName}': {e.Message}");
            }
        }

        /// <summary>
        /// Removes a layer from Unity's layer system.
        /// </summary>
        /// <param name="layerName">The name of the layer to remove</param>
        /// <returns>Response object with operation result</returns>
        public static object RemoveLayer(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return Response.Error("Layer name cannot be empty or whitespace.");

            SerializedObject tagManager = GetTagManager();
            if (tagManager == null) 
                return Response.Error("Could not access TagManager asset.");

            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (layersProp == null || !layersProp.isArray)
                return Response.Error("Could not find 'layers' property in TagManager.");

            // Find the layer by name (must be user layer)
            int layerIndexToRemove = FindLayerIndex(layersProp, layerName);
            if (layerIndexToRemove == -1)
            {
                return Response.Error($"User layer '{layerName}' not found.");
            }

            try
            {
                SerializedProperty targetLayerSP = layersProp.GetArrayElementAtIndex(layerIndexToRemove);
                targetLayerSP.stringValue = string.Empty;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                return Response.Success($"Layer '{layerName}' (slot {layerIndexToRemove}) removed successfully.");
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to remove layer '{layerName}': {e.Message}");
            }
        }

        /// <summary>
        /// Retrieves all current layers in Unity's layer system.
        /// </summary>
        /// <returns>Response object with layer dictionary</returns>
        public static object GetLayers()
        {
            try
            {
                var layers = new Dictionary<int, string>();
                for (int i = 0; i < TotalLayerCount; i++)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        layers.Add(i, layerName);
                    }
                }
                return Response.Success("Retrieved current named layers.", layers);
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to retrieve layers: {e.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the SerializedObject for the TagManager asset.
        /// </summary>
        /// <returns>SerializedObject for TagManager or null if not found</returns>
        private static SerializedObject GetTagManager()
        {
            try
            {
                UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
                if (tagManagerAssets == null || tagManagerAssets.Length == 0)
                {
                    Debug.LogError("[ManageEditorUtility] TagManager.asset not found in ProjectSettings.");
                    return null;
                }
                return new SerializedObject(tagManagerAssets[0]);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageEditorUtility] Error accessing TagManager.asset: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds the first empty layer slot in the user layer range.
        /// </summary>
        /// <param name="layersProp">The layers SerializedProperty</param>
        /// <returns>Index of first empty slot or -1 if none found</returns>
        private static int FindFirstEmptyLayerSlot(SerializedProperty layersProp)
        {
            for (int i = FirstUserLayerIndex; i < TotalLayerCount; i++)
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                if (layerSP != null && string.IsNullOrEmpty(layerSP.stringValue))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Finds the index of a layer by name in the user layer range.
        /// </summary>
        /// <param name="layersProp">The layers SerializedProperty</param>
        /// <param name="layerName">The layer name to find</param>
        /// <returns>Index of the layer or -1 if not found</returns>
        private static int FindLayerIndex(SerializedProperty layersProp, string layerName)
        {
            for (int i = FirstUserLayerIndex; i < TotalLayerCount; i++)
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                if (layerSP != null && layerName.Equals(layerSP.stringValue, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Gets the name of the active custom tool.
        /// </summary>
        /// <returns>Name of active custom tool or default message</returns>
        private static string GetActiveCustomToolName()
        {
            if (UnityEditor.Tools.current == Tool.Custom)
            {
                return "Unknown Custom Tool";
            }
            return UnityEditor.Tools.current.ToString();
        }

        #endregion
    }

    #region Data Structures

    /// <summary>
    /// Data structure for Unity Editor state information.
    /// </summary>
    public struct EditorStateData
    {
        public bool IsPlaying { get; set; }
        public bool IsPaused { get; set; }
        public bool IsCompiling { get; set; }
        public bool IsUpdating { get; set; }
        public string ApplicationPath { get; set; }
        public string ApplicationContentsPath { get; set; }
        public double TimeSinceStartup { get; set; }
    }

    /// <summary>
    /// Data structure for Unity Editor window information.
    /// </summary>
    public struct EditorWindowData
    {
        public string Title { get; set; }
        public string TypeName { get; set; }
        public bool IsFocused { get; set; }
        public WindowPositionData Position { get; set; }
        public int InstanceID { get; set; }
    }

    /// <summary>
    /// Data structure for window position information.
    /// </summary>
    public struct WindowPositionData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    /// <summary>
    /// Data structure for active Unity tool information.
    /// </summary>
    public struct ActiveToolData
    {
        public string ActiveTool { get; set; }
        public bool IsCustom { get; set; }
        public string PivotMode { get; set; }
        public string PivotRotation { get; set; }
        public Vector3 HandleRotation { get; set; }
        public Vector3 HandlePosition { get; set; }
    }

    /// <summary>
    /// Data structure for Unity Editor selection information.
    /// </summary>
    public struct SelectionData
    {
        public string ActiveObject { get; set; }
        public string ActiveGameObject { get; set; }
        public string ActiveTransform { get; set; }
        public int ActiveInstanceID { get; set; }
        public int Count { get; set; }
        public List<SelectionObjectData> Objects { get; set; }
        public List<SelectionGameObjectData> GameObjects { get; set; }
        public List<string> AssetGUIDs { get; set; }
    }

    /// <summary>
    /// Data structure for selected object information.
    /// </summary>
    public struct SelectionObjectData
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int InstanceID { get; set; }
    }

    /// <summary>
    /// Data structure for selected GameObject information.
    /// </summary>
    public struct SelectionGameObjectData
    {
        public string Name { get; set; }
        public int InstanceID { get; set; }
    }

    #endregion
}