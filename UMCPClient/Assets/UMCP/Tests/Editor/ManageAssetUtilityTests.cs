using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System.IO;
using UMCP.Editor.Tools.Utilities;
using UMCP.Editor.Helpers;

namespace UMCP.Tests.Editor
{
    /// <summary>
    /// Unit tests for ManageAssetUtility functionality.
    /// Tests asset management operations in a functional manner.
    /// </summary>
    public class ManageAssetUtilityTests
    {
        private const string TestAssetsPath = "Assets/UMCP/Tests/Editor/TestAssets";
        private const string TestMaterialPath = "Assets/UMCP/Tests/Editor/TestAssets/TestMaterial.mat";
        private const string TestFolderPath = "Assets/UMCP/Tests/Editor/TestAssets/TestFolder";

        [SetUp]
        public void SetUp()
        {
            // Ensure test directory exists
            if (!AssetDatabase.IsValidFolder(TestAssetsPath))
            {
                string parentPath = Path.GetDirectoryName(TestAssetsPath);
                string folderName = Path.GetFileName(TestAssetsPath);
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
            
            // Clean up any existing test assets
            CleanupTestAssets();
        }

        [TearDown]
        public void TearDown()
        {
            CleanupTestAssets();
        }

        private void CleanupTestAssets()
        {
            try
            {
                // Delete material asset if it exists
                if (AssetDatabase.LoadAssetAtPath<Material>(TestMaterialPath) != null)
                {
                    AssetDatabase.DeleteAsset(TestMaterialPath);
                }
                
                // Delete test folder if it exists  
                if (AssetDatabase.IsValidFolder(TestFolderPath))
                {
                    AssetDatabase.DeleteAsset(TestFolderPath);
                }
                
                // Also delete the parent TestAssets folder contents to ensure clean state
                string testAssetsDir = "Assets/UMCP/Tests/Editor/TestAssets";
                if (AssetDatabase.IsValidFolder(testAssetsDir))
                {
                    string[] assets = AssetDatabase.FindAssets("", new[] { testAssetsDir });
                    foreach (string guid in assets)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (assetPath != testAssetsDir) // Don't delete the folder itself
                        {
                            AssetDatabase.DeleteAsset(assetPath);
                        }
                    }
                }
                
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Error during test cleanup: {ex.Message}");
            }
        }

        #region Path Sanitization Tests

        [Test]
        [Description("Tests that SanitizeAssetPath correctly adds Assets/ prefix to relative paths")]
        public void SanitizeAssetPath_AddsAssetsPrefix_WhenMissing()
        {
            // Arrange
            string inputPath = "TestFolder/TestFile.txt";
            string expectedPath = "Assets/TestFolder/TestFile.txt";

            // Act
            string result = ManageAssetUtility.SanitizeAssetPath(inputPath);

            // Assert
            Assert.That(result, Is.EqualTo(expectedPath), "Should add Assets/ prefix to relative paths");
        }

        [Test]
        [Description("Tests that SanitizeAssetPath normalizes path separators")]
        public void SanitizeAssetPath_NormalizesPathSeparators_ForBackslashes()
        {
            // Arrange
            string inputPath = "Assets\\TestFolder\\TestFile.txt";
            string expectedPath = "Assets/TestFolder/TestFile.txt";

            // Act
            string result = ManageAssetUtility.SanitizeAssetPath(inputPath);

            // Assert
            Assert.That(result, Is.EqualTo(expectedPath), "Should normalize backslashes to forward slashes");
        }

        [Test]
        [Description("Tests that SanitizeAssetPath handles null and empty strings correctly")]
        public void SanitizeAssetPath_HandlesNullAndEmpty_Correctly()
        {
            // Test null input
            string nullResult = ManageAssetUtility.SanitizeAssetPath(null);
            Assert.That(nullResult, Is.Null, "Should return null for null input");

            // Test empty input
            string emptyResult = ManageAssetUtility.SanitizeAssetPath("");
            Assert.That(emptyResult, Is.EqualTo(""), "Should return empty string for empty input");
        }

        #endregion

        #region Asset Existence Tests

        [Test]
        [Description("Tests that AssetExists correctly identifies existing folders")]
        public void AssetExists_ReturnsTrueForValidFolder()
        {
            // Arrange - TestAssetsPath should exist from SetUp
            
            // Act
            bool result = ManageAssetUtility.AssetExists(TestAssetsPath);

            // Assert
            Assert.That(result, Is.True, "Should return true for existing valid folder");
        }

        [Test]
        [Description("Tests that AssetExists returns false for non-existent paths")]
        public void AssetExists_ReturnsFalseForNonExistentPath()
        {
            // Arrange
            string nonExistentPath = "Assets/NonExistent/Path.txt";

            // Act
            bool result = ManageAssetUtility.AssetExists(nonExistentPath);

            // Assert
            Assert.That(result, Is.False, "Should return false for non-existent path");
        }

        #endregion

        #region Action Validation Tests

        [Test]
        [Description("Tests that IsValidAction correctly identifies valid actions")]
        public void IsValidAction_ReturnsTrueForValidActions()
        {
            // Arrange
            string[] validActions = { "create", "modify", "delete", "duplicate", "move", "rename", "search", "get_info", "create_folder", "get_components", "import" };

            // Act & Assert
            foreach (string action in validActions)
            {
                bool result = ManageAssetUtility.IsValidAction(action);
                Assert.That(result, Is.True, $"Should return true for valid action: {action}");
            }
        }

        [Test]
        [Description("Tests that IsValidAction returns false for invalid actions")]
        public void IsValidAction_ReturnsFalseForInvalidActions()
        {
            // Arrange
            string[] invalidActions = { "invalid", "unknown", "", null };

            // Act & Assert
            foreach (string action in invalidActions)
            {
                bool result = ManageAssetUtility.IsValidAction(action);
                Assert.That(result, Is.False, $"Should return false for invalid action: {action ?? "null"}");
            }
        }

        #endregion

        #region Folder Creation Tests

        [Test]
        [Description("Tests that CreateFolder successfully creates a new folder")]
        public void CreateFolder_CreatesNewFolder_WhenPathDoesNotExist()
        {
            // Arrange
            string testPath = TestFolderPath;
            
            // Ensure folder doesn't exist
            if (AssetDatabase.IsValidFolder(testPath))
            {
                AssetDatabase.DeleteAsset(testPath);
            }

            // Act
            var result = ManageAssetUtility.CreateFolder(testPath);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Parse the response (following the Response pattern)
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            // Debug output if test fails
            if (!success)
            {
                string errorMsg = resultJson["error"]?.ToString() ?? "No error message";
                UnityEngine.Debug.LogError($"CreateFolder failed: {errorMsg}");
            }
            
            Assert.That(success, Is.True, "Should return success response");
            Assert.That(AssetDatabase.IsValidFolder(testPath), Is.True, "Folder should exist after creation");
        }

        [Test]
        [Description("Tests that CreateFolder handles existing folders gracefully")]
        public void CreateFolder_HandlesExistingFolder_Gracefully()
        {
            // Arrange
            string testPath = TestFolderPath;
            
            // Create folder first
            ManageAssetUtility.CreateFolder(testPath);
            Assert.That(AssetDatabase.IsValidFolder(testPath), Is.True, "Test folder should exist");

            // Act - try to create the same folder again
            var result = ManageAssetUtility.CreateFolder(testPath);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response even for existing folder");
        }

        [Test]
        [Description("Tests that CreateFolder returns error for invalid path")]
        public void CreateFolder_ReturnsError_ForInvalidPath()
        {
            // Act
            var result = ManageAssetUtility.CreateFolder(null);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? true;
            
            Assert.That(success, Is.False, "Should return error for null path");
        }

        #endregion

        #region Asset Creation Tests

        [Test]
        [Description("Tests that CreateAsset successfully creates a material asset")]
        public void CreateAsset_CreatesMaterial_WithValidParameters()
        {
            // Arrange
            var createData = new AssetCreationData
            {
                Path = TestMaterialPath,
                AssetType = "material",
                Properties = new JObject
                {
                    ["shader"] = "Standard"
                }
            };

            // Act
            var result = ManageAssetUtility.CreateAsset(createData);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            Debug.Log($"CreateAsset result: {resultJson}");
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            // Debug output if test fails
            if (!success)
            {
                string errorMsg = resultJson["error"]?.ToString() ?? "No error message";
                UnityEngine.Debug.LogError($"CreateAsset failed: {errorMsg}");
            }
            
            Assert.That(success, Is.True, "Should return success response");
            
            // Verify the asset was created
            var material = AssetDatabase.LoadAssetAtPath<Material>(TestMaterialPath);
            Assert.That(material, Is.Not.Null, "Material should be created at the specified path");
            Assert.That(material.shader.name, Is.EqualTo("Standard"), "Material should use the Standard shader");
        }

        [Test]
        [Description("Tests that CreateAsset returns error for missing required parameters")]
        public void CreateAsset_ReturnsError_ForMissingParameters()
        {
            // Test missing path
            var createDataNoPath = new AssetCreationData
            {
                Path = null,
                AssetType = "material",
                Properties = null
            };

            var resultNoPath = ManageAssetUtility.CreateAsset(createDataNoPath);
            var resultJsonNoPath = JObject.FromObject(resultNoPath);
            bool successNoPath = resultJsonNoPath["success"]?.ToObject<bool>() ?? true;
            Assert.That(successNoPath, Is.False, "Should return error for missing path");

            // Test missing assetType
            var createDataNoType = new AssetCreationData
            {
                Path = TestMaterialPath,
                AssetType = null,
                Properties = null
            };

            var resultNoType = ManageAssetUtility.CreateAsset(createDataNoType);
            var resultJsonNoType = JObject.FromObject(resultNoType);
            bool successNoType = resultJsonNoType["success"]?.ToObject<bool>() ?? true;
            Assert.That(successNoType, Is.False, "Should return error for missing assetType");
        }

        #endregion

        #region Asset Information Tests

        [Test]
        [Description("Tests that GetAssetInfo returns correct information for existing assets")]
        public void GetAssetInfo_ReturnsCorrectInfo_ForExistingAsset()
        {
            // Arrange - Create a test material first
            var createData = new AssetCreationData
            {
                Path = TestMaterialPath,
                AssetType = "material",
                Properties = null
            };
            ManageAssetUtility.CreateAsset(createData);

            // Act
            var result = ManageAssetUtility.GetAssetInfo(TestMaterialPath, false);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            var assetData = resultJson["data"];
            Assert.That(assetData, Is.Not.Null, "Should include asset data");
            Assert.That(assetData["path"]?.ToString(), Is.EqualTo(TestMaterialPath), "Should return correct path");
            Assert.That(assetData["name"]?.ToString(), Is.EqualTo("TestMaterial"), "Should return correct name");
        }

        [Test]
        [Description("Tests that GetAssetInfo returns error for non-existent assets")]
        public void GetAssetInfo_ReturnsError_ForNonExistentAsset()
        {
            // Arrange
            string nonExistentPath = "Assets/NonExistent.mat";

            // Act
            var result = ManageAssetUtility.GetAssetInfo(nonExistentPath, false);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? true;
            
            Assert.That(success, Is.False, "Should return error for non-existent asset");
        }

        #endregion

        #region Asset Deletion Tests

        [Test]
        [Description("Tests that DeleteAsset successfully removes existing assets")]
        public void DeleteAsset_RemovesAsset_WhenAssetExists()
        {
            // Arrange - Create a test material first
            var createData = new AssetCreationData
            {
                Path = TestMaterialPath,
                AssetType = "material",
                Properties = null
            };
            ManageAssetUtility.CreateAsset(createData);
            
            // Verify it exists
            Assert.That(ManageAssetUtility.AssetExists(TestMaterialPath), Is.True, "Test material should exist before deletion");

            // Act
            var result = ManageAssetUtility.DeleteAsset(TestMaterialPath);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            // Debug output if test fails
            if (!success)
            {
                string errorMsg = resultJson["error"]?.ToString() ?? "No error message";
                UnityEngine.Debug.LogError($"DeleteAsset failed: {errorMsg}");
            }
            
            Assert.That(success, Is.True, "Should return success response");
            Assert.That(ManageAssetUtility.AssetExists(TestMaterialPath), Is.False, "Asset should be deleted");
        }

        [Test]
        [Description("Tests that DeleteAsset returns error for non-existent assets")]
        public void DeleteAsset_ReturnsError_ForNonExistentAsset()
        {
            // Arrange
            string nonExistentPath = "Assets/NonExistent.mat";

            // Act
            var result = ManageAssetUtility.DeleteAsset(nonExistentPath);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            var resultJson = JObject.FromObject(result);
            bool success = resultJson["success"]?.ToObject<bool>() ?? true;
            
            Assert.That(success, Is.False, "Should return error for non-existent asset");
        }

        #endregion

        #region Asset Search Data Structure Tests

        [Test]
        [Description("Tests that AssetSearchData structure handles default values correctly")]
        public void AssetSearchData_HandlesDefaults_Correctly()
        {
            // Arrange & Act
            var searchData = new AssetSearchData
            {
                SearchPattern = "test",
                PageSize = 25,
                PageNumber = 2,
                GeneratePreview = true
            };

            // Assert
            Assert.That(searchData.SearchPattern, Is.EqualTo("test"), "Should store search pattern correctly");
            Assert.That(searchData.PageSize, Is.EqualTo(25), "Should store page size correctly");
            Assert.That(searchData.PageNumber, Is.EqualTo(2), "Should store page number correctly");
            Assert.That(searchData.GeneratePreview, Is.True, "Should store preview flag correctly");
        }

        #endregion
    }
}