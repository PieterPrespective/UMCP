using UnityEngine;
using UnityEditor;
using System.Linq;

namespace UMCP.Tests.Editor.Integration
{
    /// <summary>
    /// Validation helper for ManageGameObjectTool integration tests.
    /// Provides MenuItem-decorated functions for client-side validation during server tests.
    /// </summary>
    public static class ManageGameObjectTestValidator
    {
        private const string VALIDATION_SUCCESS = "VALIDATION_SUCCESS";
        private const string VALIDATION_FAILED = "VALIDATION_FAILED";
        private const string LogPrefix = "ManageGameObjectTestValidator:";

        #region GameObject Existence Validation

        [MenuItem("UMCP/Tests/ManageGameObject/ValidateGameObjectExists")]
        public static void ValidateGameObjectExists()
        {
            string objectName = "TestCreatedObject"; // This will be created by server test
            GameObject obj = GameObject.Find(objectName);
            
            if (obj != null)
            {
                Debug.Log($"{LogPrefix} {VALIDATION_SUCCESS}: GameObject '{objectName}' exists at position {obj.transform.position}");
            }
            else
            {
                Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: GameObject '{objectName}' not found");
            }
        }

        [MenuItem("UMCP/Tests/ManageGameObject/ValidateGameObjectDeleted")]
        public static void ValidateGameObjectDeleted()
        {
            string objectName = ManageGameObjectIntegrationTest.TestConstants.TestCubeName;
            GameObject obj = GameObject.Find(objectName);
            
            if (obj == null)
            {
                Debug.Log($"{LogPrefix} {VALIDATION_SUCCESS}: GameObject '{objectName}' has been deleted");
            }
            else
            {
                Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: GameObject '{objectName}' still exists");
            }
        }

        [MenuItem("UMCP/Tests/ManageGameObject/ValidateMultipleObjectsFound")]
        public static void ValidateMultipleObjectsFound()
        {
            // Find all objects with the test tag
            GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(ManageGameObjectIntegrationTest.TestConstants.TestTag);
            
            if (taggedObjects.Length > 0)
            {
                Debug.Log($"{LogPrefix} {VALIDATION_SUCCESS}: Found {taggedObjects.Length} objects with tag '{ManageGameObjectIntegrationTest.TestConstants.TestTag}'");
                foreach (var obj in taggedObjects)
                {
                    Debug.Log($"{LogPrefix}   - {obj.name} at position {obj.transform.position}");
                }
            }
            else
            {
                Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: No objects found with tag '{ManageGameObjectIntegrationTest.TestConstants.TestTag}'");
            }
        }

        #endregion

        #region GameObject Modification Validation

        [MenuItem("UMCP/Tests/ManageGameObject/ValidateNameChanged")]
        public static void ValidateNameChanged()
        {
            string newName = "ModifiedTestCube";
            GameObject obj = GameObject.Find(newName);
            
            if (obj != null)
            {
                Debug.Log($"{LogPrefix} {VALIDATION_SUCCESS}: GameObject renamed to '{newName}'");
            }
            else
            {
                // Check if original name still exists
                GameObject originalObj = GameObject.Find(ManageGameObjectIntegrationTest.TestConstants.TestCubeName);
                if (originalObj != null)
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: GameObject still has original name '{ManageGameObjectIntegrationTest.TestConstants.TestCubeName}'");
                }
                else
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: Neither original nor new named object found");
                }
            }
        }

        [MenuItem("UMCP/Tests/ManageGameObject/ValidateTransformModified")]
        public static void ValidateTransformModified()
        {
            GameObject obj = GameObject.Find(ManageGameObjectIntegrationTest.TestConstants.TestSphereName);
            
            if (obj != null)
            {
                Vector3 expectedPosition = new Vector3(5, 3, 1);
                float distance = Vector3.Distance(obj.transform.position, expectedPosition);
                
                if (distance < 0.01f)
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_SUCCESS}: Transform modified correctly to position {obj.transform.position}");
                }
                else
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: Transform position is {obj.transform.position}, expected approximately {expectedPosition}");
                }
            }
            else
            {
                Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: GameObject '{ManageGameObjectIntegrationTest.TestConstants.TestSphereName}' not found");
            }
        }

        [MenuItem("UMCP/Tests/ManageGameObject/ValidateActiveStateChanged")]
        public static void ValidateActiveStateChanged()
        {
            GameObject obj = GameObject.Find(ManageGameObjectIntegrationTest.TestConstants.TestSphereName);
            
            if (obj != null)
            {
                if (!obj.activeSelf)
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_SUCCESS}: GameObject is inactive as expected");
                }
                else
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: GameObject is still active");
                }
            }
            else
            {
                // Try to find inactive objects
                GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                var inactiveObj = allObjects.FirstOrDefault(o => o.name == ManageGameObjectIntegrationTest.TestConstants.TestSphereName);
                
                if (inactiveObj != null && !inactiveObj.activeSelf)
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_SUCCESS}: GameObject found and is inactive");
                }
                else
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: GameObject not found or state incorrect");
                }
            }
        }

        #endregion

        #region Component Validation

        [MenuItem("UMCP/Tests/ManageGameObject/ValidateComponentAdded")]
        public static void ValidateComponentAdded()
        {
            GameObject obj = GameObject.Find(ManageGameObjectIntegrationTest.TestConstants.TestCubeName);
            
            if (obj != null)
            {
                var audioSource = obj.GetComponent<AudioSource>();
                if (audioSource != null)
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_SUCCESS}: AudioSource component added successfully");
                    Debug.Log($"{LogPrefix}   Volume: {audioSource.volume}, Loop: {audioSource.loop}");
                }
                else
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: AudioSource component not found on object");
                }
            }
            else
            {
                Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: GameObject '{ManageGameObjectIntegrationTest.TestConstants.TestCubeName}' not found");
            }
        }

        [MenuItem("UMCP/Tests/ManageGameObject/ValidateComponentRemoved")]
        public static void ValidateComponentRemoved()
        {
            GameObject obj = GameObject.Find(ManageGameObjectIntegrationTest.TestConstants.ComponentTestObjectName);
            
            if (obj != null)
            {
                var boxCollider = obj.GetComponent<BoxCollider>();
                if (boxCollider == null)
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_SUCCESS}: BoxCollider component removed successfully");
                    
                    // Check if Rigidbody still exists
                    var rigidbody = obj.GetComponent<Rigidbody>();
                    if (rigidbody != null)
                    {
                        Debug.Log($"{LogPrefix}   Rigidbody component still present (as expected)");
                    }
                }
                else
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: BoxCollider component still exists on object");
                }
            }
            else
            {
                Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: GameObject '{ManageGameObjectIntegrationTest.TestConstants.ComponentTestObjectName}' not found");
            }
        }

        [MenuItem("UMCP/Tests/ManageGameObject/ValidateComponentPropertyModified")]
        public static void ValidateComponentPropertyModified()
        {
            GameObject obj = GameObject.Find(ManageGameObjectIntegrationTest.TestConstants.ComponentTestObjectName);
            
            if (obj != null)
            {
                var rigidbody = obj.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    float expectedMass = 10f;
                    bool expectedGravity = true;
                    
                    if (Mathf.Approximately(rigidbody.mass, expectedMass) && rigidbody.useGravity == expectedGravity)
                    {
                        Debug.Log($"{LogPrefix} {VALIDATION_SUCCESS}: Rigidbody properties modified correctly");
                        Debug.Log($"{LogPrefix}   Mass: {rigidbody.mass}, UseGravity: {rigidbody.useGravity}");
                    }
                    else
                    {
                        Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: Rigidbody properties incorrect");
                        Debug.Log($"{LogPrefix}   Mass: {rigidbody.mass} (expected: {expectedMass}), UseGravity: {rigidbody.useGravity} (expected: {expectedGravity})");
                    }
                }
                else
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: Rigidbody component not found on object");
                }
            }
            else
            {
                Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: GameObject '{ManageGameObjectIntegrationTest.TestConstants.ComponentTestObjectName}' not found");
            }
        }

        [MenuItem("UMCP/Tests/ManageGameObject/ValidateComponentsList")]
        public static void ValidateComponentsList()
        {
            GameObject obj = GameObject.Find(ManageGameObjectIntegrationTest.TestConstants.ComponentTestObjectName);
            
            if (obj != null)
            {
                var components = obj.GetComponents<Component>();
                Debug.Log($"{LogPrefix} {VALIDATION_SUCCESS}: Found {components.Length} components on '{obj.name}':");
                
                foreach (var component in components)
                {
                    Debug.Log($"{LogPrefix}   - {component.GetType().Name}");
                }
            }
            else
            {
                Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: GameObject '{ManageGameObjectIntegrationTest.TestConstants.ComponentTestObjectName}' not found");
            }
        }

        #endregion

        #region Hierarchy Validation

        [MenuItem("UMCP/Tests/ManageGameObject/ValidateParentChildHierarchy")]
        public static void ValidateParentChildHierarchy()
        {
            GameObject parent = GameObject.Find(ManageGameObjectIntegrationTest.TestConstants.ParentObjectName);
            GameObject child = GameObject.Find(ManageGameObjectIntegrationTest.TestConstants.ChildObjectName);
            
            if (parent != null && child != null)
            {
                if (child.transform.parent == parent.transform)
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_SUCCESS}: Parent-child hierarchy is correct");
                    Debug.Log($"{LogPrefix}   Parent: {parent.name}, Child: {child.name}");
                    Debug.Log($"{LogPrefix}   Child local position: {child.transform.localPosition}");
                }
                else
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: Child is not parented to the correct object");
                }
            }
            else
            {
                Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: Parent or child object not found");
                if (parent == null) Debug.Log($"{LogPrefix}   Parent '{ManageGameObjectIntegrationTest.TestConstants.ParentObjectName}' missing");
                if (child == null) Debug.Log($"{LogPrefix}   Child '{ManageGameObjectIntegrationTest.TestConstants.ChildObjectName}' missing");
            }
        }

        [MenuItem("UMCP/Tests/ManageGameObject/ValidateNewParentSet")]
        public static void ValidateNewParentSet()
        {
            GameObject sphere = GameObject.Find(ManageGameObjectIntegrationTest.TestConstants.TestSphereName);
            GameObject newParent = GameObject.Find(ManageGameObjectIntegrationTest.TestConstants.ParentObjectName);
            
            if (sphere != null && newParent != null)
            {
                if (sphere.transform.parent == newParent.transform)
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_SUCCESS}: Object successfully reparented");
                    Debug.Log($"{LogPrefix}   '{sphere.name}' is now child of '{newParent.name}'");
                }
                else
                {
                    Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: Object was not reparented correctly");
                }
            }
            else
            {
                Debug.Log($"{LogPrefix} {VALIDATION_FAILED}: Required objects not found for reparenting validation");
            }
        }

        #endregion

        #region Utility Functions

        [MenuItem("UMCP/Tests/ManageGameObject/LogCurrentSceneState")]
        public static void LogCurrentSceneState()
        {
            Debug.Log($"{LogPrefix} Current scene state:");
            
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            int testObjectCount = 0;
            
            foreach (var root in rootObjects)
            {
                if (root.name.StartsWith("ManageGO_") || root.name == "TestCreatedObject" || root.name == "ModifiedTestCube")
                {
                    testObjectCount++;
                    LogGameObjectDetails(root, 0);
                }
            }
            
            Debug.Log($"{LogPrefix} Total test objects in scene: {testObjectCount}");
        }

        private static void LogGameObjectDetails(GameObject obj, int depth)
        {
            string indent = new string(' ', depth * 2);
            string activeState = obj.activeSelf ? "Active" : "Inactive";
            
            Debug.Log($"{LogPrefix} {indent}- {obj.name} ({activeState}, Tag: {obj.tag}, Layer: {obj.layer})");
            Debug.Log($"{LogPrefix} {indent}  Position: {obj.transform.position}, Rotation: {obj.transform.rotation.eulerAngles}, Scale: {obj.transform.localScale}");
            
            var components = obj.GetComponents<Component>();
            if (components.Length > 1) // More than just Transform
            {
                Debug.Log($"{LogPrefix} {indent}  Components: {string.Join(", ", System.Array.ConvertAll(components, c => c.GetType().Name))}");
            }
            
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                LogGameObjectDetails(obj.transform.GetChild(i).gameObject, depth + 1);
            }
        }

        [MenuItem("UMCP/Tests/ManageGameObject/CreateTestPrimitiveForValidation")]
        public static void CreateTestPrimitiveForValidation()
        {
            GameObject testObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testObj.name = "ValidationTestCube";
            testObj.transform.position = new Vector3(10, 0, 0);
            
            Debug.Log($"{LogPrefix} Created ValidationTestCube for testing find operations");
        }

        [MenuItem("UMCP/Tests/ManageGameObject/CleanupValidationTestObjects")]
        public static void CleanupValidationTestObjects()
        {
            // Clean up any test-created objects
            string[] testObjectNames = { "TestCreatedObject", "ModifiedTestCube", "ValidationTestCube" };
            
            foreach (string name in testObjectNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                    Debug.Log($"{LogPrefix} Cleaned up test object: {name}");
                }
            }
            
            Debug.Log($"{LogPrefix} Validation test cleanup completed");
        }

        #endregion
    }
}