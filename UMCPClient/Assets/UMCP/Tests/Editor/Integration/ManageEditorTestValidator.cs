using UnityEngine;
using UnityEditor;
using System.Linq;

namespace UMCP.Tests.Editor.Integration
{
    /// <summary>
    /// Helper class providing validation methods for ManageEditor integration tests.
    /// These methods can be invoked via ExecuteMenuItem to validate editor state changes.
    /// </summary>
    public static class ManageEditorTestValidator
    {
        private const string MenuPrefix = "UMCP/Tests/ManageEditor/";

        [MenuItem(MenuPrefix + "Validate Tag Exists")]
        public static void ValidateTagExists()
        {
            string tagName = ManageEditorIntegrationTest.TestConstants.TagName;
            bool exists = UnityEditorInternal.InternalEditorUtility.tags.Contains(tagName);
            
            if (exists)
            {
                Debug.Log($"VALIDATION_SUCCESS: Tag '{tagName}' exists");
            }
            else
            {
                Debug.LogError($"VALIDATION_FAILED: Tag '{tagName}' does not exist");
            }
        }

        [MenuItem(MenuPrefix + "Validate Tag Removed")]
        public static void ValidateTagRemoved()
        {
            string tagName = ManageEditorIntegrationTest.TestConstants.TagName;
            bool exists = UnityEditorInternal.InternalEditorUtility.tags.Contains(tagName);
            
            if (!exists)
            {
                Debug.Log($"VALIDATION_SUCCESS: Tag '{tagName}' was removed");
            }
            else
            {
                Debug.LogError($"VALIDATION_FAILED: Tag '{tagName}' still exists");
            }
        }

        [MenuItem(MenuPrefix + "Validate Layer Exists")]
        public static void ValidateLayerExists()
        {
            string layerName = ManageEditorIntegrationTest.TestConstants.LayerName;
            int layerIndex = LayerMask.NameToLayer(layerName);
            
            if (layerIndex >= 0)
            {
                Debug.Log($"VALIDATION_SUCCESS: Layer '{layerName}' exists at index {layerIndex}");
            }
            else
            {
                Debug.LogError($"VALIDATION_FAILED: Layer '{layerName}' does not exist");
            }
        }

        [MenuItem(MenuPrefix + "Validate Layer Removed")]
        public static void ValidateLayerRemoved()
        {
            string layerName = ManageEditorIntegrationTest.TestConstants.LayerName;
            int layerIndex = LayerMask.NameToLayer(layerName);
            
            if (layerIndex < 0)
            {
                Debug.Log($"VALIDATION_SUCCESS: Layer '{layerName}' was removed");
            }
            else
            {
                Debug.LogError($"VALIDATION_FAILED: Layer '{layerName}' still exists at index {layerIndex}");
            }
        }

        [MenuItem(MenuPrefix + "Validate Play Mode Active")]
        public static void ValidatePlayModeActive()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.Log("VALIDATION_SUCCESS: Play mode is active");
            }
            else
            {
                Debug.LogError("VALIDATION_FAILED: Play mode is not active");
            }
        }

        [MenuItem(MenuPrefix + "Validate Play Mode Inactive")]
        public static void ValidatePlayModeInactive()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("VALIDATION_SUCCESS: Play mode is inactive");
            }
            else
            {
                Debug.LogError("VALIDATION_FAILED: Play mode is still active");
            }
        }

        [MenuItem(MenuPrefix + "Validate Selection")]
        public static void ValidateSelection()
        {
            string expectedName = ManageEditorIntegrationTest.TestConstants.TestObjectName;
            GameObject selected = Selection.activeGameObject;
            
            if (selected != null && selected.name == expectedName)
            {
                Debug.Log($"VALIDATION_SUCCESS: Correct object selected: {expectedName}");
            }
            else if (selected != null)
            {
                Debug.LogError($"VALIDATION_FAILED: Wrong object selected. Expected: {expectedName}, Got: {selected.name}");
            }
            else
            {
                Debug.LogError($"VALIDATION_FAILED: No object selected. Expected: {expectedName}");
            }
        }

        [MenuItem(MenuPrefix + "Log Editor State")]
        public static void LogEditorState()
        {
            Debug.Log($"EDITOR_STATE: IsPlaying={EditorApplication.isPlaying}");
            Debug.Log($"EDITOR_STATE: IsPaused={EditorApplication.isPaused}");
            Debug.Log($"EDITOR_STATE: IsCompiling={EditorApplication.isCompiling}");
            Debug.Log($"EDITOR_STATE: IsPlayingOrWillChangePlaymode={EditorApplication.isPlayingOrWillChangePlaymode}");
            
            // Log active tool
            Debug.Log($"EDITOR_STATE: ActiveTool={Tools.current}");
            
            // Log selection count
            Debug.Log($"EDITOR_STATE: SelectionCount={Selection.objects.Length}");
            
            // Log tag count
            var tags = UnityEditorInternal.InternalEditorUtility.tags;
            Debug.Log($"EDITOR_STATE: TagCount={tags.Length}");
            
            // Log layer count
            int layerCount = 0;
            for (int i = 0; i < 32; i++)
            {
                if (!string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                {
                    layerCount++;
                }
            }
            Debug.Log($"EDITOR_STATE: LayerCount={layerCount}");
        }

        [MenuItem(MenuPrefix + "Validate Tool Changed")]
        public static void ValidateToolChanged()
        {
            // Check if the tool is set to Rotate (as an example)
            if (Tools.current == Tool.Rotate)
            {
                Debug.Log($"VALIDATION_SUCCESS: Tool changed to Rotate");
            }
            else
            {
                Debug.LogError($"VALIDATION_FAILED: Tool is {Tools.current}, expected Rotate");
            }
        }

        [MenuItem(MenuPrefix + "Create Additional Test Tag")]
        public static void CreateAdditionalTestTag()
        {
            string additionalTag = "ManageEditorTestTag2";
            
            if (!UnityEditorInternal.InternalEditorUtility.tags.Contains(additionalTag))
            {
                UnityEditorInternal.InternalEditorUtility.AddTag(additionalTag);
                Debug.Log($"TEST_ACTION: Created additional tag '{additionalTag}'");
            }
            else
            {
                Debug.Log($"TEST_ACTION: Additional tag '{additionalTag}' already exists");
            }
        }

        [MenuItem(MenuPrefix + "Clean Additional Test Tag")]
        public static void CleanAdditionalTestTag()
        {
            string additionalTag = "ManageEditorTestTag2";
            
            if (UnityEditorInternal.InternalEditorUtility.tags.Contains(additionalTag))
            {
                UnityEditorInternal.InternalEditorUtility.RemoveTag(additionalTag);
                Debug.Log($"TEST_ACTION: Removed additional tag '{additionalTag}'");
            }
            else
            {
                Debug.Log($"TEST_ACTION: Additional tag '{additionalTag}' does not exist");
            }
        }
    }
}