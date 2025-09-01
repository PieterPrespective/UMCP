#if UMCP_INTEGRATION_TEST
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace UMCP.Tests.Editor.Integration
{
    /// <summary>
    /// Validation helper for ManageAssetTool integration tests.
    /// Provides MenuItem-decorated functions for server-side test validation.
    /// </summary>
    public static class ManageAssetTestValidator
    {
        private const string ValidationPrefix = "[ManageAssetValidation]";
        
        // Asset Creation Validation - FIXED PATH TO MATCH SERVER TEST
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Validate Material Created")]
        public static void ValidateMaterialCreated()
        {
            // NOTE: This path MUST match what the server-side test creates
            string path = "Assets/UMCP_Test_Assets/NewTestMaterial2.mat"; // Fixed to match server test
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            
            if (material != null)
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_SUCCESS [TestCreateMaterialAsset]: Material created at {path}");
                Debug.Log($"{ValidationPrefix} Material name: {material.name}, Shader: {material.shader.name}");
            }
            else
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_FAILED [TestCreateMaterialAsset]: Material not found at {path}");
            }
        }
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Validate Folder Created")]
        public static void ValidateFolderCreated()
        {
            string path = "Assets/UMCP_Test_Assets/NewTestFolder";
            bool folderExists = AssetDatabase.IsValidFolder(path);
            
            if (folderExists)
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_SUCCESS [TestCreateFolder]: Folder created at {path}");
            }
            else
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_FAILED [TestCreateFolder]: Folder not found at {path}");
            }
        }
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Validate ScriptableObject Created")]
        public static void ValidateScriptableObjectCreated()
        {
            string path = "Assets/UMCP_Test_Assets/NewScriptableObject.asset";
            ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            
            if (so != null)
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_SUCCESS: ScriptableObject created at {path}");
                Debug.Log($"{ValidationPrefix} ScriptableObject type: {so.GetType().Name}");
            }
            else
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_FAILED: ScriptableObject not found at {path}");
            }
        }
        
        // Asset Modification Validation
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Validate Asset Modified")]
        public static void ValidateAssetModified()
        {
            string path = "Assets/UMCP_Test_Assets/TestMaterial.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            
            if (material != null)
            {
                // Check if the material color was modified (we expect it to be changed from red)
                if (material.color != Color.red)
                {
                    Debug.Log($"{ValidationPrefix} VALIDATION_SUCCESS [TestModifyAsset]: Material modified - Color is now {material.color}");
                }
                else
                {
                    Debug.Log($"{ValidationPrefix} VALIDATION_FAILED [TestModifyAsset]: Material not modified - Color is still red");
                }
            }
            else
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_FAILED [TestModifyAsset]: Material not found at {path}");
            }
        }
        
        // Asset Deletion Validation
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Validate Asset Deleted")]
        public static void ValidateAssetDeleted()
        {
            string path = "Assets/UMCP_Test_Assets/TestTexture.png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            
            if (texture == null)
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_SUCCESS: Asset deleted from {path}");
            }
            else
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_FAILED: Asset still exists at {path}");
            }
        }
        
        // Asset Duplication Validation
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Validate Asset Duplicated")]
        public static void ValidateAssetDuplicated()
        {
            string originalPath = "Assets/UMCP_Test_Assets/TestPrefab.prefab";
            string duplicatePath = "Assets/UMCP_Test_Assets/TestPrefab_Copy.prefab";
            
            GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(originalPath);
            GameObject duplicate = AssetDatabase.LoadAssetAtPath<GameObject>(duplicatePath);
            
            if (original != null && duplicate != null)
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_SUCCESS [TestDuplicateAsset]: Asset duplicated from {originalPath} to {duplicatePath}");
            }
            else
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_FAILED [TestDuplicateAsset]: Duplication failed - Original exists: {original != null}, Duplicate exists: {duplicate != null}");
            }
        }
        
        // Asset Move/Rename Validation
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Validate Asset Moved")]
        public static void ValidateAssetMoved()
        {
            string oldPath = "Assets/UMCP_Test_Assets/TestScriptableObject.asset";
            string newPath = "Assets/UMCP_Test_Assets/TestSubFolder/MovedScriptableObject.asset";
            
            ScriptableObject oldAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(oldPath);
            ScriptableObject newAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(newPath);
            
            if (oldAsset == null && newAsset != null)
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_SUCCESS [TestMoveAsset]: Asset moved from {oldPath} to {newPath}");
            }
            else
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_FAILED [TestMoveAsset]: Move failed - Old exists: {oldAsset != null}, New exists: {newAsset != null}");
            }
        }
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Validate Asset Renamed")]
        public static void ValidateAssetRenamed()
        {
            string oldPath = "Assets/UMCP_Test_Assets/SearchTests/SearchMaterial_1.mat";
            string newPath = "Assets/UMCP_Test_Assets/SearchTests/RenamedMaterial.mat";
            
            Material oldAsset = AssetDatabase.LoadAssetAtPath<Material>(oldPath);
            Material newAsset = AssetDatabase.LoadAssetAtPath<Material>(newPath);
            
            if (oldAsset == null && newAsset != null)
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_SUCCESS [TestRenameAsset]: Asset renamed from SearchMaterial_1 to RenamedMaterial");
            }
            else
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_FAILED [TestRenameAsset]: Rename failed - Old exists: {oldAsset != null}, New exists: {newAsset != null}");
            }
        }
        
        // Asset Search Validation
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Validate Search Results")]
        public static void ValidateSearchResults()
        {
            // Search for all materials in the test folder
            string searchFolder = "Assets/UMCP_Test_Assets/SearchTests";
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { searchFolder });
            
            if (guids.Length > 0)
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_SUCCESS [TestSearchAssets]: Found {guids.Length} materials in search folder");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Debug.Log($"{ValidationPrefix} Found material: {path}");
                }
            }
            else
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_FAILED [TestSearchAssets]: No materials found in search folder");
            }
        }
        
        // Asset Info Validation
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Validate Asset Info")]
        public static void ValidateAssetInfo()
        {
            string path = "Assets/UMCP_Test_Assets/TestMaterial.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            
            if (material != null)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                System.Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                int instanceId = material.GetInstanceID();
                
                Debug.Log($"{ValidationPrefix} VALIDATION_SUCCESS [TestGetAssetInfo]: Asset info retrieved");
                Debug.Log($"{ValidationPrefix} Path: {path}");
                Debug.Log($"{ValidationPrefix} GUID: {guid}");
                Debug.Log($"{ValidationPrefix} Type: {type.Name}");
                Debug.Log($"{ValidationPrefix} Instance ID: {instanceId}");
            }
            else
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_FAILED [TestGetAssetInfo]: Asset not found for info retrieval");
            }
        }
        
        // Component Info Validation
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Validate Components Info")]
        public static void ValidateComponentsInfo()
        {
            string path = "Assets/UMCP_Test_Assets/TestPrefab.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab != null)
            {
                Component[] components = prefab.GetComponents<Component>();
                if (components.Length > 0)
                {
                    Debug.Log($"{ValidationPrefix} VALIDATION_SUCCESS [TestGetComponents]: Found {components.Length} components");
                    foreach (Component comp in components)
                    {
                        Debug.Log($"{ValidationPrefix} Component: {comp.GetType().Name}");
                    }
                }
                else
                {
                    Debug.Log($"{ValidationPrefix} VALIDATION_FAILED [TestGetComponents]: No components found on prefab");
                }
            }
            else
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_FAILED [TestGetComponents]: Prefab not found for component info");
            }
        }
        
        // Import/Reimport Validation
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Validate Asset Reimported")]
        public static void ValidateAssetReimported()
        {
            string path = "Assets/UMCP_Test_Assets/TestTexture.png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            
            if (texture != null)
            {
                // Check import settings or modification time
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    Debug.Log($"{ValidationPrefix} VALIDATION_SUCCESS [TestImportAsset]: Asset reimported");
                    Debug.Log($"{ValidationPrefix} Import settings - MaxSize: {importer.maxTextureSize}, Compression: {importer.textureCompression}");
                }
                else
                {
                    Debug.Log($"{ValidationPrefix} VALIDATION_FAILED [TestImportAsset]: Could not get importer for asset");
                }
            }
            else
            {
                Debug.Log($"{ValidationPrefix} VALIDATION_FAILED [TestImportAsset]: Asset not found for reimport validation"); 
            }
        }
        
        // Utility Functions - REMOVED CLEANUP THAT COULD DELETE FILES
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Log Asset Database State")]
        public static void LogAssetDatabaseState()
        {
            Debug.Log($"{ValidationPrefix} === Asset Database State ===");
            
            string testFolder = "Assets/UMCP_Test_Assets";
            if (AssetDatabase.IsValidFolder(testFolder))
            {
                string[] allAssets = AssetDatabase.FindAssets("", new[] { testFolder });
                Debug.Log($"{ValidationPrefix} Total assets in test folder: {allAssets.Length}");
                
                foreach (string guid in allAssets)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    System.Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                    Debug.Log($"{ValidationPrefix} Asset: {path} (Type: {type?.Name ?? "Unknown"})");
                }
            }
            else
            {
                Debug.Log($"{ValidationPrefix} Test folder does not exist");
            }
        }
        
        [MenuItem("UMCP/Integration Tests/ManageAsset/Create Test Asset For Validation")]
        public static void CreateTestAssetForValidation()
        {
            string folderPath = "Assets/UMCP_Test_Assets";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "UMCP_Test_Assets");
            }
            
            Material mat = new Material(Shader.Find("Standard"));
            mat.name = "ValidationTestMaterial";
            mat.color = Color.yellow;
            AssetDatabase.CreateAsset(mat, folderPath + "/ValidationTestMaterial.mat");
            AssetDatabase.SaveAssets();
            
            Debug.Log($"{ValidationPrefix} Created validation test material at {folderPath}/ValidationTestMaterial.mat");
        }
        
        // REMOVED THE PROBLEMATIC CLEANUP FUNCTION THAT COULD DELETE FILES INCLUDING ITSELF
        // This function was using AssetDatabase.DeleteAsset() which could potentially delete the validator file
    }
}
#endif