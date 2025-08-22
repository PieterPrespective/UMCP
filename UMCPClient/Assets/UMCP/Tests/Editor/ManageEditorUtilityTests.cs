using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using UMCP.Editor.Tools.Utilities;
using UMCP.Editor.Helpers;
using System.Collections;
using UnityEngine.TestTools;
using System.Diagnostics;

namespace UMCP.Tests.Editor
{
    /// <summary>
    /// Unit tests for ManageEditorUtility functionality.
    /// Tests Unity Editor management operations in a functional manner.
    /// </summary>
    public class ManageEditorUtilityTests
    {
        private List<string> _addedTags;
        private List<string> _addedLayers;

        [SetUp]
        public void SetUp()
        {
            _addedTags = new List<string>();
            _addedLayers = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up added tags
            foreach (string tag in _addedTags)
            {
                try
                {
                    if (InternalEditorUtility.tags.Contains(tag))
                    {
                        InternalEditorUtility.RemoveTag(tag);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            _addedTags.Clear();

            // Clean up added layers
            foreach (string layer in _addedLayers)
            {
                try
                {
                    var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                    var layersProp = tagManager.FindProperty("layers");
                    
                    for (int i = 8; i < 32; i++) // User layers only
                    {
                        var layerSP = layersProp.GetArrayElementAtIndex(i);
                        if (layerSP != null && layer.Equals(layerSP.stringValue))
                        {
                            layerSP.stringValue = string.Empty;
                            tagManager.ApplyModifiedProperties();
                            break;
                        }
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            _addedLayers.Clear();
        }

        #region Action Validation Tests

        [Test]
        [Description("Tests that IsValidAction correctly identifies valid actions")]
        public void IsValidAction_ReturnsTrueForValidActions()
        {
            // Arrange
            string[] validActions = { 
                "play", "pause", "stop", "get_state", "get_windows", "get_active_tool", 
                "get_selection", "set_active_tool", "add_tag", "remove_tag", "get_tags", 
                "add_layer", "remove_layer", "get_layers"
            };

            // Act & Assert
            foreach (string action in validActions)
            {
                bool result = ManageEditorUtility.IsValidAction(action);
                Assert.That(result, Is.True, $"Should return true for valid action: {action}");
            }
        }

        [Test]
        [Description("Tests that IsValidAction returns false for invalid actions")]
        public void IsValidAction_ReturnsFalseForInvalidActions()
        {
            // Arrange
            string[] invalidActions = { "invalid", "unknown", "create_folder", "refresh_assets", "", null };

            // Act & Assert
            foreach (string action in invalidActions)
            {
                bool result = ManageEditorUtility.IsValidAction(action);
                Assert.That(result, Is.False, $"Should return false for invalid action: {action ?? "null"}");
            }
        }

        #endregion

        #region Play Mode Control Tests

        [UnityTest]
        [Description("Tests that PlayMode returns success result")]
        public IEnumerator PlayMode_ReturnsSuccessResult()
        {
            // Act
            var result = ManageEditorUtility.PlayMode();


            UnityEngine.Debug.Log("PlayMode result: " + result);
            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            Assert.That(resultJson["message"], Is.Not.Null, "Should include a message");

            EditorPrefs.HasKey("UnityEditor.EditorApplication.isPlaying");

            if(hasDomainReloadEnabled())
            {
                yield return new WaitForDomainReload();
            }

            Assert.That(EditorApplication.isPlaying, Is.True, "Editor should be in playing state after PlayMode");

            EditorApplication.ExitPlaymode();
            
            yield return null; // Ensure coroutine completes
        }

        private static bool hasDomainReloadEnabled()
        {
            // Get the enter play mode options flags
            EnterPlayModeOptions options = EditorSettings.enterPlayModeOptions;

            // Check if Enter Play Mode Options is enabled at all
            bool isEnabled = EditorSettings.enterPlayModeOptionsEnabled;

            return (options & EnterPlayModeOptions.DisableDomainReload) == 0;
        }

        [UnityTest]
        [Description("Tests that PauseMode returns appropriate result based on play state")]
        public IEnumerator PauseMode_ReturnsAppropriateResult()
        {
            // First enter play mode to test pause functionality
            EditorApplication.EnterPlaymode();
            
            if(hasDomainReloadEnabled())
            {
                yield return new WaitForDomainReload();
            }
            
            yield return null; // Wait one frame for play mode to start
            
            // Act
            var result = ManageEditorUtility.PauseMode();

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            // Should return success when in play mode
            Assert.That(success, Is.True, "Should return success when pausing from play mode");
            Assert.That(resultJson["message"], Is.Not.Null, "Should include a message");
            
            // Clean up - exit play mode
            EditorApplication.ExitPlaymode();
            yield return null;
        }

        [UnityTest]
        [Description("Tests that StopMode returns success result")]
        public IEnumerator StopMode_ReturnsSuccessResult()
        {
            // First enter play mode so we can test stopping
            EditorApplication.EnterPlaymode();
            
            if(hasDomainReloadEnabled())
            {
                yield return new WaitForDomainReload();
            }
            
            yield return null; // Wait one frame for play mode to start
            
            // Act
            var result = ManageEditorUtility.StopMode();

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            Assert.That(resultJson["message"], Is.Not.Null, "Should include a message");
            
            // Wait for stop mode to take effect
            yield return null;
            
            // Verify we're no longer in play mode
            Assert.That(EditorApplication.isPlaying, Is.False, "Should not be in play mode after StopMode");
        }

        #endregion

        #region Editor State Tests

        [Test]
        [Description("Tests that GetEditorState returns editor state information")]
        public void GetEditorState_ReturnsEditorStateInformation()
        {
            // Act
            var result = ManageEditorUtility.GetEditorState();

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            var resultData = resultJson["data"];
            Assert.That(resultData, Is.Not.Null, "Should include result data");
            Assert.That(resultData["IsPlaying"], Is.Not.Null, "Should include IsPlaying field");
            Assert.That(resultData["IsPaused"], Is.Not.Null, "Should include IsPaused field");
            Assert.That(resultData["IsCompiling"], Is.Not.Null, "Should include IsCompiling field");
        }

        [Test]
        [Description("Tests that GetEditorWindows returns window information")]
        public void GetEditorWindows_ReturnsWindowInformation()
        {
            // Act
            var result = ManageEditorUtility.GetEditorWindows();

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            var resultData = resultJson["data"] as JArray;
            Assert.That(resultData, Is.Not.Null, "Should include result data as array");
            // Should have at least some windows open during testing
            Assert.That(resultData.Count, Is.GreaterThanOrEqualTo(0), "Should return window list");
        }

        [Test]
        [Description("Tests that GetActiveTool returns tool information")]
        public void GetActiveTool_ReturnsToolInformation()
        {
            // Act
            var result = ManageEditorUtility.GetActiveTool();

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Use a JsonSerializerSettings to handle circular references
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            
            var resultJson = JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(settings));
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            var resultData = resultJson["data"];
            Assert.That(resultData, Is.Not.Null, "Should include result data");
            Assert.That(resultData["ActiveTool"], Is.Not.Null, "Should include ActiveTool field");
            Assert.That(resultData["IsCustom"], Is.Not.Null, "Should include IsCustom field");
        }

        [Test]
        [Description("Tests that SetActiveTool handles valid tool names")]
        public void SetActiveTool_HandlesValidToolNames()
        {
            // Arrange
            string[] validTools = { "Move", "Rotate", "Scale", "Transform" };

            foreach (string toolName in validTools)
            {
                // Act
                var result = ManageEditorUtility.SetActiveTool(toolName);

                // Assert
                Assert.That(result, Is.Not.Null, $"Should return a result object for tool: {toolName}");
                
                var resultJson = JObject.FromObject(result);
                bool success = resultJson["success"]?.ToObject<bool>() ?? false;
                
                Assert.That(success, Is.True, $"Should return success response for tool: {toolName}");
            }
        }

        [Test]
        [Description("Tests that SetActiveTool returns error for invalid tool names")]
        public void SetActiveTool_ReturnsErrorForInvalidToolNames()
        {
            // Arrange
            string[] invalidTools = { "InvalidTool", "None", "", null };

            foreach (string toolName in invalidTools)
            {
                // Act
                var result = ManageEditorUtility.SetActiveTool(toolName);

                // Assert
                Assert.That(result, Is.Not.Null, $"Should return a result object for tool: {toolName ?? "null"}");
                
                var resultJson = JObject.FromObject(result);
                bool success = resultJson["success"]?.ToObject<bool>() ?? true;
                
                Assert.That(success, Is.False, $"Should return error response for tool: {toolName ?? "null"}");
            }
        }

        [Test]
        [Description("Tests that GetSelection returns selection information")]
        public void GetSelection_ReturnsSelectionInformation()
        {
            // Act
            var result = ManageEditorUtility.GetSelection();

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            var resultData = resultJson["data"];
            Assert.That(resultData, Is.Not.Null, "Should include result data");
            Assert.That(resultData["Count"], Is.Not.Null, "Should include Count field");
            Assert.That(resultData["Objects"], Is.Not.Null, "Should include Objects field");
        }

        #endregion

        #region Tag Management Tests

        [Test]
        [Description("Tests that AddTag successfully adds a new tag")]
        public void AddTag_AddsNewTag_Successfully()
        {
            // Arrange
            string testTag = "TestTag_" + System.Guid.NewGuid().ToString("N")[..8];

            // Act
            var result = ManageEditorUtility.AddTag(testTag);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            Assert.That(InternalEditorUtility.tags.Contains(testTag), Is.True, "Tag should be added to Unity's tag system");
            
            _addedTags.Add(testTag);
        }

        [Test]
        [Description("Tests that AddTag returns error for empty tag name")]
        public void AddTag_ReturnsError_ForEmptyTagName()
        {
            // Act
            var result = ManageEditorUtility.AddTag("");

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? true;
            
            Assert.That(success, Is.False, "Should return error status for empty tag name");
        }

        [Test]
        [Description("Tests that AddTag returns error for duplicate tag")]
        public void AddTag_ReturnsError_ForDuplicateTag()
        {
            // Arrange
            string testTag = "TestDuplicateTag_" + System.Guid.NewGuid().ToString("N")[..8];
            ManageEditorUtility.AddTag(testTag);
            _addedTags.Add(testTag);

            // Act
            var result = ManageEditorUtility.AddTag(testTag);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? true;
            
            Assert.That(success, Is.False, "Should return error status for duplicate tag");
        }

        [Test]
        [Description("Tests that RemoveTag successfully removes an existing tag")]
        public void RemoveTag_RemovesExistingTag_Successfully()
        {
            // Arrange
            string testTag = "TestRemoveTag_" + System.Guid.NewGuid().ToString("N")[..8];
            ManageEditorUtility.AddTag(testTag);
            _addedTags.Add(testTag);

            // Act
            var result = ManageEditorUtility.RemoveTag(testTag);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            Assert.That(InternalEditorUtility.tags.Contains(testTag), Is.False, "Tag should be removed from Unity's tag system");
            
            _addedTags.Remove(testTag);
        }

        [Test]
        [Description("Tests that RemoveTag returns error for non-existent tag")]
        public void RemoveTag_ReturnsError_ForNonExistentTag()
        {
            // Arrange
            string nonExistentTag = "NonExistentTag_" + System.Guid.NewGuid().ToString("N")[..8];

            // Act
            var result = ManageEditorUtility.RemoveTag(nonExistentTag);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? true;
            
            Assert.That(success, Is.False, "Should return error status for non-existent tag");
        }

        [Test]
        [Description("Tests that RemoveTag prevents removing built-in Untagged tag")]
        public void RemoveTag_PreventsRemovingUntagged()
        {
            // Act
            var result = ManageEditorUtility.RemoveTag("Untagged");

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? true;
            
            Assert.That(success, Is.False, "Should return error status when trying to remove Untagged");
        }

        [Test]
        [Description("Tests that GetTags returns current tag list")]
        public void GetTags_ReturnsCurrentTagList()
        {
            // Act
            var result = ManageEditorUtility.GetTags();

            UnityEngine.Debug.Log($"GetTags result: {result}");

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            var tags = resultJson["data"] as JArray;
            Assert.That(tags, Is.Not.Null, "Should include tags array");
            Assert.That(tags.Count, Is.GreaterThanOrEqualTo(1), "Should have at least the default Untagged tag");
        }

        #endregion

        #region Layer Management Tests

        [Test]
        [Description("Tests that AddLayer successfully adds a new layer")]
        public void AddLayer_AddsNewLayer_Successfully()
        {
            // Arrange
            string testLayer = "TestLayer_" + System.Guid.NewGuid().ToString("N")[..8];

            // Act
            var result = ManageEditorUtility.AddLayer(testLayer);

            UnityEngine.Debug.Log($"AddLayer result: {result}");


            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            // Verify the layer was actually added
            bool layerExists = false;
            for (int i = 8; i < 32; i++)
            {
                if (LayerMask.LayerToName(i) == testLayer)
                {
                    layerExists = true;
                    break;
                }
            }
            Assert.That(layerExists, Is.True, "Layer should be added to Unity's layer system");
            
            _addedLayers.Add(testLayer);
        }

        [Test]
        [Description("Tests that AddLayer returns error for empty layer name")]
        public void AddLayer_ReturnsError_ForEmptyLayerName()
        {
            // Act
            var result = ManageEditorUtility.AddLayer("");

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? true;
            
            Assert.That(success, Is.False, "Should return error status for empty layer name");
        }

        [Test]
        [Description("Tests that RemoveLayer successfully removes an existing layer")]
        public void RemoveLayer_RemovesExistingLayer_Successfully()
        {
            // Arrange
            string testLayer = "TestRemoveLayer_" + System.Guid.NewGuid().ToString("N")[..8];
            ManageEditorUtility.AddLayer(testLayer);
            _addedLayers.Add(testLayer);

            // Act
            var result = ManageEditorUtility.RemoveLayer(testLayer);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            // Verify the layer was actually removed
            bool layerExists = false;
            for (int i = 8; i < 32; i++)
            {
                if (LayerMask.LayerToName(i) == testLayer)
                {
                    layerExists = true;
                    break;
                }
            }
            Assert.That(layerExists, Is.False, "Layer should be removed from Unity's layer system");
            
            _addedLayers.Remove(testLayer);
        }

        [Test]
        [Description("Tests that GetLayers returns current layer dictionary")]
        public void GetLayers_ReturnsCurrentLayerDictionary()
        {
            // Act
            var result = ManageEditorUtility.GetLayers();

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            var layers = resultJson["data"] as JObject;
            Assert.That(layers, Is.Not.Null, "Should include layers object");
            
            // Should have at least the built-in layers
            Assert.That(layers.Count, Is.GreaterThanOrEqualTo(8), "Should have at least the built-in layers (0-7)");
        }

        #endregion

        #region Data Structure Tests

        [Test]
        [Description("Tests that EditorStateData handles editor state correctly")]
        public void EditorStateData_HandlesEditorState_Correctly()
        {
            // Arrange & Act
            var editorState = new EditorStateData
            {
                IsPlaying = true,
                IsPaused = false,
                IsCompiling = false,
                IsUpdating = false,
                ApplicationPath = "/path/to/unity",
                ApplicationContentsPath = "/path/to/contents",
                TimeSinceStartup = 123.45
            };

            // Assert
            Assert.That(editorState.IsPlaying, Is.EqualTo(true), "Should store IsPlaying correctly");
            Assert.That(editorState.IsPaused, Is.EqualTo(false), "Should store IsPaused correctly");
            Assert.That(editorState.ApplicationPath, Is.EqualTo("/path/to/unity"), "Should store ApplicationPath correctly");
            Assert.That(editorState.TimeSinceStartup, Is.EqualTo(123.45).Within(0.001), "Should store TimeSinceStartup correctly");
        }

        [Test]
        [Description("Tests that WindowPositionData handles position correctly")]
        public void WindowPositionData_HandlesPosition_Correctly()
        {
            // Arrange & Act
            var position = new WindowPositionData
            {
                X = 100.5f,
                Y = 200.5f,
                Width = 800.0f,
                Height = 600.0f
            };

            // Assert
            Assert.That(position.X, Is.EqualTo(100.5f).Within(0.001f), "Should store X correctly");
            Assert.That(position.Y, Is.EqualTo(200.5f).Within(0.001f), "Should store Y correctly");
            Assert.That(position.Width, Is.EqualTo(800.0f).Within(0.001f), "Should store Width correctly");
            Assert.That(position.Height, Is.EqualTo(600.0f).Within(0.001f), "Should store Height correctly");
        }

        [Test]
        [Description("Tests that ActiveToolData handles tool information correctly")]
        public void ActiveToolData_HandlesToolInformation_Correctly()
        {
            // Arrange & Act
            var toolData = new ActiveToolData
            {
                ActiveTool = "Move",
                IsCustom = false,
                PivotMode = "Pivot",
                PivotRotation = "Local",
                HandleRotation = new Vector3(0, 90, 0),
                HandlePosition = new Vector3(1, 2, 3)
            };

            // Assert
            Assert.That(toolData.ActiveTool, Is.EqualTo("Move"), "Should store ActiveTool correctly");
            Assert.That(toolData.IsCustom, Is.EqualTo(false), "Should store IsCustom correctly");
            Assert.That(toolData.PivotMode, Is.EqualTo("Pivot"), "Should store PivotMode correctly");
            Assert.That(toolData.HandleRotation, Is.EqualTo(new Vector3(0, 90, 0)), "Should store HandleRotation correctly");
            Assert.That(toolData.HandlePosition, Is.EqualTo(new Vector3(1, 2, 3)), "Should store HandlePosition correctly");
        }

        #endregion
    }
}