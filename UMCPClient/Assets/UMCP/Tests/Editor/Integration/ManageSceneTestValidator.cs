using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;

namespace UMCP.Tests.Editor.Integration
{
    /// <summary>
    /// Validation helper for ManageScene integration tests.
    /// Contains MenuItem-decorated methods for server-side test validation.
    /// </summary>
    public static class ManageSceneTestValidator
    {
        private const string VALIDATION_SUCCESS = "VALIDATION_SUCCESS";
        private const string VALIDATION_FAILED = "VALIDATION_FAILED";

        /// <summary>
        /// Validates that a test scene was created successfully
        /// </summary>
        [MenuItem("UMCP/Test Validation/ManageScene/Validate Scene Created")]
        public static void ValidateSceneCreated()
        {
            string scenePath = ManageSceneIntegrationTest.TestConstants.TestScenePath;
            string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), scenePath);
            
            if (File.Exists(fullPath))
            {
                Debug.Log($"{VALIDATION_SUCCESS} [Validate Scene Created]: ManageSceneTestValidator - Scene created successfully at: {scenePath}");
            }
            else
            {
                Debug.Log($"{VALIDATION_FAILED} [Validate Scene Created]: ManageSceneTestValidator - Scene not found at: {scenePath}");
            }
        }

        /// <summary>
        /// Validates that a scene was loaded successfully by checking active scene
        /// </summary>
        [MenuItem("UMCP/Test Validation/ManageScene/Validate Scene Loaded")]
        public static void ValidateSceneLoaded()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            string expectedName = ManageSceneIntegrationTest.TestConstants.TestSceneName;
            
            if (activeScene.name == expectedName && activeScene.isLoaded)
            {
                Debug.Log($"{VALIDATION_SUCCESS} [Validate Scene Loaded]: ManageSceneTestValidator - Scene loaded successfully: {activeScene.name} at {activeScene.path}");
            }
            else
            {
                Debug.Log($"{VALIDATION_FAILED} [Validate Scene Loaded]: ManageSceneTestValidator - Expected scene '{expectedName}' not loaded. Current: {activeScene.name}");
            }
        }

        /// <summary>
        /// Validates that the current scene was saved successfully (not dirty)
        /// </summary>
        [MenuItem("UMCP/Test Validation/ManageScene/Validate Scene Saved")]
        public static void ValidateSceneSaved()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            
            if (!activeScene.isDirty && !string.IsNullOrEmpty(activeScene.path))
            {
                Debug.Log($"{VALIDATION_SUCCESS} [Validate Scene Saved]: ManageSceneTestValidator - Scene saved successfully: {activeScene.name} at {activeScene.path}");
            }
            else if (activeScene.isDirty)
            {
                Debug.Log($"{VALIDATION_FAILED} [Validate Scene Saved]: ManageSceneTestValidator - Scene is still dirty (unsaved): {activeScene.name}");
            }
            else
            {
                Debug.Log($"{VALIDATION_FAILED} [Validate Scene Saved]: ManageSceneTestValidator - Scene has no path (not saved): {activeScene.name}");
            }
        }

        /// <summary>
        /// Validates that scene hierarchy contains expected test objects
        /// </summary>
        [MenuItem("UMCP/Test Validation/ManageScene/Validate Scene Hierarchy")]
        public static void ValidateSceneHierarchy()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();
            
            // Look for test hierarchy root
            GameObject testRoot = rootObjects.FirstOrDefault(go => go.name == ManageSceneIntegrationTest.TestConstants.TestHierarchyRootName);
            
            if (testRoot != null)
            {
                int childCount = testRoot.transform.childCount;
                bool hasTestChild1 = testRoot.transform.Find(ManageSceneIntegrationTest.TestConstants.TestChild1Name) != null;
                bool hasTestChild2 = testRoot.transform.Find(ManageSceneIntegrationTest.TestConstants.TestChild2Name) != null;
                
                if (childCount >= 2 && hasTestChild1 && hasTestChild2)
                {
                    Debug.Log($"{VALIDATION_SUCCESS} [Validate Scene Hierarchy]: ManageSceneTestValidator - Scene hierarchy valid with {childCount} children, found test objects");
                }
                else
                {
                    Debug.Log($"{VALIDATION_FAILED} [Validate Scene Hierarchy]: ManageSceneTestValidator - Scene hierarchy incomplete. Children: {childCount}, TestChild1: {hasTestChild1}, TestChild2: {hasTestChild2}");
                }
            }
            else
            {
                Debug.Log($"{VALIDATION_FAILED} [Validate Scene Hierarchy]: ManageSceneTestValidator - Test hierarchy root not found in scene. Root objects: {string.Join(", ", rootObjects.Select(go => go.name))}");
            }
        }

        /// <summary>
        /// Validates active scene information matches expectations
        /// </summary>
        [MenuItem("UMCP/Test Validation/ManageScene/Validate Active Scene Info")]
        public static void ValidateActiveSceneInfo()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                int rootCount = activeScene.rootCount;
                string sceneName = activeScene.name;
                string scenePath = activeScene.path;
                int buildIndex = activeScene.buildIndex;
                
                Debug.Log($"{VALIDATION_SUCCESS} [Validate Active Scene Info]: ManageSceneTestValidator - Active scene info: Name='{sceneName}', Path='{scenePath}', BuildIndex={buildIndex}, RootCount={rootCount}, IsLoaded={activeScene.isLoaded}, IsDirty={activeScene.isDirty}");
            }
            else
            {
                Debug.Log($"{VALIDATION_FAILED} [Validate Active Scene Info]: ManageSceneTestValidator - No valid active scene found");
            }
        }

        /// <summary>
        /// Validates build settings contain scenes
        /// </summary>
        [MenuItem("UMCP/Test Validation/ManageScene/Validate Build Settings")]
        public static void ValidateBuildSettings()
        {
            int sceneCount = EditorBuildSettings.scenes.Length;
            int enabledScenes = EditorBuildSettings.scenes.Count(scene => scene.enabled);
            
            if (sceneCount > 0)
            {
                var scenes = EditorBuildSettings.scenes.Take(3).Select(scene => new { 
                    Path = scene.path, 
                    Enabled = scene.enabled,
                    GUID = scene.guid.ToString()
                });
                
                string scenesInfo = string.Join(", ", scenes.Select(s => $"'{Path.GetFileNameWithoutExtension(s.Path)}'({(s.Enabled ? "enabled" : "disabled")})"));
                
                Debug.Log($"{VALIDATION_SUCCESS} [Validate Build Settings]: ManageSceneTestValidator - Build settings: {sceneCount} total scenes, {enabledScenes} enabled. First scenes: {scenesInfo}");
            }
            else
            {
                Debug.Log($"{VALIDATION_FAILED} [Validate Build Settings]: ManageSceneTestValidator - No scenes found in build settings");
            }
        }

        /// <summary>
        /// Logs comprehensive scene state for debugging
        /// </summary>
        [MenuItem("UMCP/Test Validation/ManageScene/Log Scene State")]
        public static void LogSceneState()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            
            Debug.Log($"ManageSceneTestValidator - Current Scene State:");
            Debug.Log($"  Name: {activeScene.name}");
            Debug.Log($"  Path: {activeScene.path}");
            Debug.Log($"  Build Index: {activeScene.buildIndex}");
            Debug.Log($"  Is Valid: {activeScene.IsValid()}");
            Debug.Log($"  Is Loaded: {activeScene.isLoaded}");
            Debug.Log($"  Is Dirty: {activeScene.isDirty}");
            Debug.Log($"  Root Count: {activeScene.rootCount}");
            Debug.Log($"  Scene Count in Build Settings: {EditorBuildSettings.scenes.Length}");
            
            // Log root objects
            GameObject[] rootObjects = activeScene.GetRootGameObjects();
            Debug.Log($"  Root Objects ({rootObjects.Length}): {string.Join(", ", rootObjects.Select(go => go.name))}");
        }

        /// <summary>
        /// Creates a test scene for validation purposes
        /// </summary>
        [MenuItem("UMCP/Test Validation/ManageScene/Create Test Scene For Validation")]
        public static void CreateTestSceneForValidation()
        {
            string sceneName = "ValidationTestScene";
            string scenePath = $"Assets/Scenes/{sceneName}.unity";
            string fullPath = Path.Combine(Application.dataPath, "Scenes", $"{sceneName}.unity");
            
            // Create directory if it doesn't exist
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Create new scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            
            // Add a test object
            GameObject testObject = new GameObject("ValidationTestObject");
            testObject.transform.position = Vector3.zero;
            
            // Save the scene
            bool saved = EditorSceneManager.SaveScene(newScene, scenePath);
            
            if (saved)
            {
                AssetDatabase.Refresh();
                Debug.Log($"ManageSceneTestValidator: Created test scene '{sceneName}' at '{scenePath}' for validation");
            }
            else
            {
                Debug.Log($"ManageSceneTestValidator: Failed to create test scene '{sceneName}'");
            }
        }

        /// <summary>
        /// Cleans up validation test scene
        /// </summary>
        [MenuItem("UMCP/Test Validation/ManageScene/Cleanup Validation Test Scene")]
        public static void CleanupValidationTestScene()
        {
            string scenePath = "Assets/Scenes/ValidationTestScene.unity";
            
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            {
                AssetDatabase.DeleteAsset(scenePath);
                Debug.Log($"ManageSceneTestValidator: Cleaned up validation test scene: {scenePath}");
            }
            else
            {
                Debug.Log($"ManageSceneTestValidator: Validation test scene not found: {scenePath}");
            }
        }
    }
}