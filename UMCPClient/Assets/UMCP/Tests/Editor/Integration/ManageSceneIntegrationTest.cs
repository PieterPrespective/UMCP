using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using NUnit.Framework;
using UMCP.Editor.Testing;
using System.IO;

namespace UMCP.Tests.Editor.Integration
{
    /// <summary>
    /// Integration test harness for testing ManageSceneTool functionality.
    /// Creates test scenes and GameObjects to validate scene management operations.
    /// </summary>
    public class ManageSceneIntegrationTest : UMCPIntegrationTestHarnass
    {
        private Scene originalScene;
        private string originalScenePath;
        private GameObject testHierarchyRoot;
        private GameObject testChild1;
        private GameObject testChild2;
        
        /// <summary>
        /// Test constants for server-side validation
        /// </summary>
        public static class TestConstants
        {
            public const string TestSceneName = "ManageSceneTestScene";
            public const string TestScenePath = "Assets/Scenes/ManageSceneTestScene.unity";
            public const string TestHierarchyRootName = "ManageSceneTestRoot";
            public const string TestChild1Name = "TestChild1";
            public const string TestChild2Name = "TestChild2";
            public const string SetupCompletedLogMessage = "ManageSceneIntegrationTest setup completed successfully";
        }

        /// <summary>
        /// Sets up the ManageScene integration test environment.
        /// Creates test scenes and hierarchy for comprehensive scene management testing.
        /// </summary>
        /// <returns>IEnumerator for Unity coroutine execution</returns>
        public override IEnumerator SetupIntegrationTest()
        {
            Debug.Log("ManageSceneIntegrationTest: Starting ManageScene integration test setup...");

            // Remember original scene state
            originalScene = EditorSceneManager.GetActiveScene();
            originalScenePath = originalScene.path;
            Debug.Log($"ManageSceneIntegrationTest: Original scene: {originalScene.name} at {originalScenePath}");

            // Ensure Scenes directory exists
            string scenesDir = Path.Combine(Application.dataPath, "Scenes");
            if (!Directory.Exists(scenesDir))
            {
                Directory.CreateDirectory(scenesDir);
                Debug.Log("ManageSceneIntegrationTest: Created Scenes directory");
            }

            // Create test hierarchy in current scene for hierarchy testing
            CreateTestHierarchy();
            
            yield return null; // Wait one frame

            // Validate test hierarchy was created properly
            Assert.That(testHierarchyRoot, Is.Not.Null, "Test hierarchy root should be created");
            Assert.That(testChild1, Is.Not.Null, "Test child 1 should be created");
            Assert.That(testChild2, Is.Not.Null, "Test child 2 should be created");
            Assert.That(testChild1.transform.parent, Is.EqualTo(testHierarchyRoot.transform), "Child 1 should be parented to root");
            Assert.That(testChild2.transform.parent, Is.EqualTo(testHierarchyRoot.transform), "Child 2 should be parented to root");

            Debug.Log("ManageSceneIntegrationTest: Test hierarchy created successfully");

            // Log completion message for server-side validation
            Debug.Log(TestConstants.SetupCompletedLogMessage);
            Debug.Log("ManageSceneIntegrationTest: Setup completed successfully");
        }

        /// <summary>
        /// Cleans up the ManageScene integration test environment.
        /// Removes test GameObjects and restores original scene state.
        /// </summary>
        /// <returns>IEnumerator for Unity coroutine execution</returns>
        public override IEnumerator CleanupIntegrationTest()
        {
            Debug.Log("ManageSceneIntegrationTest: Starting cleanup...");

            // Clean up test hierarchy
            CleanupTestHierarchy();
            
            yield return null; // Wait one frame

            // Clean up any test scenes that might have been created
            CleanupTestScenes();

            // Try to restore original scene if it's different from current
            Scene currentScene = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(originalScenePath) && 
                currentScene.path != originalScenePath && 
                File.Exists(Path.Combine(Application.dataPath.Replace("Assets", ""), originalScenePath)))
            {
                try
                {
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                    Debug.Log($"ManageSceneIntegrationTest: Restored original scene: {originalScenePath}");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"ManageSceneIntegrationTest: Could not restore original scene: {e.Message}");
                }
            }

            yield return null; // Wait one frame

            Debug.Log("ManageSceneIntegrationTest: Cleanup completed");
        }

        /// <summary>
        /// Creates test hierarchy for scene hierarchy testing
        /// </summary>
        private void CreateTestHierarchy()
        {
            // Create root GameObject
            testHierarchyRoot = new GameObject(TestConstants.TestHierarchyRootName);
            testHierarchyRoot.transform.position = Vector3.zero;

            // Create child GameObjects
            testChild1 = new GameObject(TestConstants.TestChild1Name);
            testChild1.transform.SetParent(testHierarchyRoot.transform);
            testChild1.transform.localPosition = new Vector3(1, 0, 0);

            testChild2 = new GameObject(TestConstants.TestChild2Name);
            testChild2.transform.SetParent(testHierarchyRoot.transform);
            testChild2.transform.localPosition = new Vector3(-1, 0, 0);

            // Add some components for testing
            testChild1.AddComponent<BoxCollider>();
            testChild2.AddComponent<Rigidbody>();

            Debug.Log("ManageSceneIntegrationTest: Test hierarchy created with root and 2 children");
        }

        /// <summary>
        /// Cleans up test hierarchy GameObjects
        /// </summary>
        private void CleanupTestHierarchy()
        {
            if (testHierarchyRoot != null)
            {
                Object.DestroyImmediate(testHierarchyRoot);
                testHierarchyRoot = null;
                Debug.Log("ManageSceneIntegrationTest: Test hierarchy root destroyed");
            }

            testChild1 = null;
            testChild2 = null;
        }

        /// <summary>
        /// Cleans up any test scenes that might have been created during testing
        /// </summary>
        private void CleanupTestScenes()
        {
            string testScenePath = TestConstants.TestScenePath;
            string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), testScenePath);
            
            if (File.Exists(fullPath))
            {
                try
                {
                    AssetDatabase.DeleteAsset(testScenePath);
                    Debug.Log($"ManageSceneIntegrationTest: Deleted test scene: {testScenePath}");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"ManageSceneIntegrationTest: Could not delete test scene: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Gets the expected log message for server-side validation.
        /// </summary>
        public static string GetExpectedLogMessage()
        {
            return TestConstants.SetupCompletedLogMessage;
        }
    }
}