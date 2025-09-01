using System.Collections;
using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using UMCP.Editor.Testing;
using System.Linq;

namespace UMCP.Tests.Editor.Integration
{
    /// <summary>
    /// Integration test harness for testing the ManageEditor functionality.
    /// Sets up test scenarios for editor operations including tags, layers, and state verification.
    /// </summary>
    public class ManageEditorIntegrationTest : UMCPIntegrationTestHarnass
    {
        private const string TestTagName = "ManageEditorTestTag";
        private const string TestLayerName = "ManageEditorTestLayer";
        private const string TestLogPrefix = "ManageEditorIntegrationTest:";
        
        private GameObject testGameObject;
        private int initialTagCount;
        private int initialLayerCount;
        private bool isPlayMode;

        /// <summary>
        /// Sets up the ManageEditor integration test environment.
        /// Creates test tags, layers, and GameObjects for validation.
        /// </summary>
        /// <returns>IEnumerator for Unity coroutine execution</returns>
        public override IEnumerator SetupIntegrationTest()
        {
            Debug.Log($"{TestLogPrefix} Starting setup...");

            // Store initial state
            isPlayMode = EditorApplication.isPlaying;
            initialTagCount = UnityEditorInternal.InternalEditorUtility.tags.Length;
            initialLayerCount = GetDefinedLayerCount();

            // Create a test GameObject
            testGameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            testGameObject.name = "ManageEditorTestObject";
            testGameObject.transform.position = Vector3.one;

            // Validate GameObject creation
            Assert.That(testGameObject, Is.Not.Null, "Test GameObject should be created successfully");
            Assert.That(testGameObject.name, Is.EqualTo("ManageEditorTestObject"), "Test GameObject should have correct name");

            yield return null;

            // Log initial editor state
            Debug.Log($"{TestLogPrefix} Editor State - IsPlaying: {EditorApplication.isPlaying}");
            Debug.Log($"{TestLogPrefix} Editor State - IsPaused: {EditorApplication.isPaused}");
            Debug.Log($"{TestLogPrefix} Editor State - IsCompiling: {EditorApplication.isCompiling}");
            Debug.Log($"{TestLogPrefix} Initial tag count: {initialTagCount}");
            Debug.Log($"{TestLogPrefix} Initial layer count: {initialLayerCount}");

            // Add test tag if it doesn't exist
            if (!TagExists(TestTagName))
            {
                AddTag(TestTagName);
                Debug.Log($"{TestLogPrefix} Added test tag: {TestTagName}");
            }
            else
            {
                Debug.Log($"{TestLogPrefix} Test tag already exists: {TestTagName}");
            }

            // Add test layer if possible
            int layerIndex = GetFirstAvailableLayer();
            if (layerIndex >= 0)
            {
                SetLayerName(layerIndex, TestLayerName);
                Debug.Log($"{TestLogPrefix} Added test layer: {TestLayerName} at index {layerIndex}");
            }
            else
            {
                Debug.LogWarning($"{TestLogPrefix} No available layer slot for test layer");
            }

            yield return null;

            // Select the test GameObject
            Selection.activeGameObject = testGameObject;
            Debug.Log($"{TestLogPrefix} Selected test GameObject");

            // Log current tags
            var tags = UnityEditorInternal.InternalEditorUtility.tags;
            Debug.Log($"{TestLogPrefix} Current tags ({tags.Length}): {string.Join(", ", tags)}");

            // Log current layers
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    Debug.Log($"{TestLogPrefix} Layer {i}: {layerName}");
                }
            }

            Debug.Log($"{TestLogPrefix} Setup completed successfully");
        }

        /// <summary>
        /// Cleans up the ManageEditor integration test environment.
        /// Removes test tags, layers, and GameObjects.
        /// </summary>
        /// <returns>IEnumerator for Unity coroutine execution</returns>
        public override IEnumerator CleanupIntegrationTest()
        {
            Debug.Log($"{TestLogPrefix} Starting cleanup...");

            // Destroy test GameObject
            if (testGameObject != null)
            {
                Object.DestroyImmediate(testGameObject);
                testGameObject = null;
                Debug.Log($"{TestLogPrefix} Test GameObject destroyed");
            }

            // Remove test tag if it exists
            if (TagExists(TestTagName))
            {
                RemoveTag(TestTagName);
                Debug.Log($"{TestLogPrefix} Removed test tag: {TestTagName}");
            }

            // Remove test layer if it exists
            int layerIndex = LayerMask.NameToLayer(TestLayerName);
            if (layerIndex >= 0)
            {
                SetLayerName(layerIndex, "");
                Debug.Log($"{TestLogPrefix} Removed test layer: {TestLayerName}");
            }

            yield return null;

            // Restore play mode state if needed
            if (isPlayMode && !EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
            }
            else if (!isPlayMode && EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            Debug.Log($"{TestLogPrefix} Cleanup completed successfully");
        }

        #region Helper Methods

        private bool TagExists(string tagName)
        {
            return UnityEditorInternal.InternalEditorUtility.tags.Contains(tagName);
        }

        private void AddTag(string tagName)
        {
            UnityEditorInternal.InternalEditorUtility.AddTag(tagName);
        }

        private void RemoveTag(string tagName)
        {
            UnityEditorInternal.InternalEditorUtility.RemoveTag(tagName);
        }

        private int GetDefinedLayerCount()
        {
            int count = 0;
            for (int i = 0; i < 32; i++)
            {
                if (!string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                {
                    count++;
                }
            }
            return count;
        }

        private int GetFirstAvailableLayer()
        {
            // Start from layer 8 (first user layer) to avoid built-in layers
            for (int i = 8; i < 32; i++)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                {
                    return i;
                }
            }
            return -1;
        }

        private void SetLayerName(int layerIndex, string layerName)
        {
            var tagManager = new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProperty = tagManager.FindProperty("layers");
            
            if (layerIndex >= 0 && layerIndex < layersProperty.arraySize)
            {
                var layerProperty = layersProperty.GetArrayElementAtIndex(layerIndex);
                layerProperty.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
            }
        }

        #endregion

        /// <summary>
        /// Gets test-specific constants for server-side validation.
        /// </summary>
        public static class TestConstants
        {
            public static string TagName => TestTagName;
            public static string LayerName => TestLayerName;
            public static string LogPrefix => TestLogPrefix;
            public static string TestObjectName => "ManageEditorTestObject";
        }
    }
}