#if UMCP_INTEGRATION_TEST
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using UMCP.Editor.Testing;
using UMCP.Tests.Player;

namespace UMCP.Tests.Editor.Integration
{
    /// <summary>
    /// Integration test harness for testing ManageAssetTool functionality.
    /// Creates test assets and folder structure for comprehensive asset management testing.
    /// </summary>
    public class ManageAssetIntegrationTest : UMCPIntegrationTestHarnass
    {
        // Test asset paths
        private const string TestFolderPath = "Assets/UMCP_Test_Assets";
        private const string TestMaterialPath = TestFolderPath + "/TestMaterial.mat";
        private const string TestTexturePath = TestFolderPath + "/TestTexture.png";
        private const string TestPrefabPath = TestFolderPath + "/TestPrefab.prefab";
        private const string TestScriptableObjectPath = TestFolderPath + "/TestScriptableObject.asset";
        private const string TestSubFolderPath = TestFolderPath + "/TestSubFolder";
        private const string TestSearchFolder = TestFolderPath + "/SearchTests";
        
        // Test objects
        private Material testMaterial;
        private Texture2D testTexture;
        private GameObject testPrefabInstance;
        private ScriptableObject testScriptableObject;
        
        // Test constants for server-side validation
        public static class TestConstants
        {
            public const string TestFolderName = "UMCP_Test_Assets";
            public const string TestMaterialName = "TestMaterial";
            public const string TestTextureName = "TestTexture";
            public const string TestPrefabName = "TestPrefab";
            public const string TestScriptableObjectName = "TestScriptableObject";
            public const string SetupCompleteMessage = "ManageAssetIntegrationTest setup completed successfully";
            public const string TestLogPrefix = "ManageAssetIntegrationTest";
        }

        /// <summary>
        /// Sets up the test environment by creating test assets and folder structure.
        /// </summary>
        public override IEnumerator SetupIntegrationTest()
        {
            //Before setting up, first make sure all testassets are deleted (in case a previous loop failed
            yield return CleanupIntegrationTest();


            Debug.Log($"[{TestConstants.TestLogPrefix}] Starting setup...");

            // Create test folder structure
            if (!AssetDatabase.IsValidFolder(TestFolderPath))
            {
                string parentFolder = "Assets";
                string folderName = TestConstants.TestFolderName;
                AssetDatabase.CreateFolder(parentFolder, folderName);
                Debug.Log($"[{TestConstants.TestLogPrefix}] Created test folder: {TestFolderPath}");
            }

            // Create subfolder for move/rename tests
            if (!AssetDatabase.IsValidFolder(TestSubFolderPath))
            {
                AssetDatabase.CreateFolder(TestFolderPath, "TestSubFolder");
                Debug.Log($"[{TestConstants.TestLogPrefix}] Created subfolder: {TestSubFolderPath}");
            }

            // Create search test folder with multiple assets
            if (!AssetDatabase.IsValidFolder(TestSearchFolder))
            {
                AssetDatabase.CreateFolder(TestFolderPath, "SearchTests");
                Debug.Log($"[{TestConstants.TestLogPrefix}] Created search folder: {TestSearchFolder}");
            }

            yield return null; // Wait a frame for folder creation

            // Create test material
            testMaterial = new Material(Shader.Find("Standard"));
            testMaterial.name = TestConstants.TestMaterialName;
            testMaterial.color = Color.red;
            AssetDatabase.CreateAsset(testMaterial, TestMaterialPath);
            Debug.Log($"[{TestConstants.TestLogPrefix}] Created test material: {TestMaterialPath}");

            // Create test texture
            testTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[64 * 64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.blue;
            }
            testTexture.SetPixels(pixels);
            testTexture.Apply();
            byte[] textureBytes = testTexture.EncodeToPNG();
            File.WriteAllBytes(Application.dataPath + "/../" + TestTexturePath, textureBytes);
            AssetDatabase.Refresh();
            Debug.Log($"[{TestConstants.TestLogPrefix}] Created test texture: {TestTexturePath}");

            // Create test prefab
            GameObject prefabObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefabObject.name = TestConstants.TestPrefabName;
            prefabObject.transform.localScale = Vector3.one * 0.5f;
            testPrefabInstance = PrefabUtility.SaveAsPrefabAsset(prefabObject, TestPrefabPath);
            Object.DestroyImmediate(prefabObject);
            Debug.Log($"[{TestConstants.TestLogPrefix}] Created test prefab: {TestPrefabPath}");

            // Create test ScriptableObject
            testScriptableObject = ScriptableObject.CreateInstance<UMCPTestScriptableAsset>();
            AssetDatabase.CreateAsset(testScriptableObject, TestScriptableObjectPath);
            Debug.Log($"[{TestConstants.TestLogPrefix}] Created test ScriptableObject: {TestScriptableObjectPath}");

            // Create additional assets for search testing
            for (int i = 1; i <= 3; i++)
            {
                Material searchMaterial = new Material(Shader.Find("Standard"));
                searchMaterial.name = $"SearchMaterial_{i}";
                searchMaterial.color = Color.green;
                string searchMaterialPath = $"{TestSearchFolder}/SearchMaterial_{i}.mat";
                AssetDatabase.CreateAsset(searchMaterial, searchMaterialPath);
                Debug.Log($"[{TestConstants.TestLogPrefix}] Created search material: {searchMaterialPath}");
            }

            // Save and refresh asset database
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            yield return null; // Wait for asset database refresh

            // Validate setup
            Assert.That(AssetDatabase.IsValidFolder(TestFolderPath), Is.True, $"Test folder should exist at {TestFolderPath}");
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(TestMaterialPath), Is.Not.Null, $"Test material should exist at {TestMaterialPath}");
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(TestTexturePath), Is.Not.Null, $"Test texture should exist at {TestTexturePath}");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabPath), Is.Not.Null, $"Test prefab should exist at {TestPrefabPath}");
            Assert.That(AssetDatabase.LoadAssetAtPath<ScriptableObject>(TestScriptableObjectPath), Is.Not.Null, $"Test ScriptableObject should exist at {TestScriptableObjectPath}");

            Debug.Log($"[{TestConstants.TestLogPrefix}] {TestConstants.SetupCompleteMessage}");
        }

        /// <summary>
        /// Cleans up the test environment by deleting all test assets and folders.
        /// </summary>
        public override IEnumerator CleanupIntegrationTest()
        {
            Debug.Log($"[{TestConstants.TestLogPrefix}] Starting cleanup...");

            // Delete test folder and all contents
            if (!string.IsNullOrEmpty(TestFolderPath) && AssetDatabase.IsValidFolder(TestFolderPath))
            {
                string[] allAssets = AssetDatabase.GetAllAssetPaths();
                foreach (string assetPath in allAssets)
                {
                    if (assetPath.StartsWith(TestFolderPath))
                    {
                        //Make sure to only delete assets deriving from UnityEngine.Object
                        if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
                        {
                            AssetDatabase.DeleteAsset(assetPath);
                            Debug.Log($"[{TestConstants.TestLogPrefix}] Deleted test asset: {assetPath}");
                        }
                    }
                }

                AssetDatabase.DeleteAsset(TestFolderPath);
                Debug.Log($"[{TestConstants.TestLogPrefix}] Deleted test folder: {TestFolderPath}");
            }

            //// Clean up any test assets that might have been moved outside the test folder
            //string[] allAssets = AssetDatabase.GetAllAssetPaths();
            //foreach (string assetPath in allAssets)
            //{
            //    if (assetPath.Contains("ManageAssetTest") || assetPath.Contains("UMCP_Test"))
            //    {

            //        //Make sure to only delete assets deriving from UnityEngine.Object
            //        if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
            //        {
            //            AssetDatabase.DeleteAsset(assetPath);
            //            Debug.Log($"[{TestConstants.TestLogPrefix}] Cleaned up test asset: {assetPath}");
            //        }
            //    }
            //}

            // Save and refresh
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            yield return null; // Wait for cleanup

            Debug.Log($"[{TestConstants.TestLogPrefix}] Cleanup completed");
        }

        /// <summary>
        /// Gets the expected setup completion message for server-side validation.
        /// </summary>
        public static string GetExpectedLogMessage()
        {
            return TestConstants.SetupCompleteMessage;
        }
    }

    
}
#endif