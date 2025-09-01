using System.Collections;
using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using UMCP.Editor.Testing;

namespace UMCP.Tests.Editor.Integration
{
    /// <summary>
    /// Integration test harness for ManageGameObjectTool server-side tests.
    /// Creates various test GameObjects, components, and hierarchies for comprehensive testing.
    /// </summary>
    public class ManageGameObjectIntegrationTest : UMCPIntegrationTestHarnass
    {
        // Test objects
        private GameObject testCube;
        private GameObject testSphere;
        private GameObject parentObject;
        private GameObject childObject;
        private GameObject prefabInstance;
        private GameObject taggedObject;
        private GameObject componentTestObject;

        // Test constants for server-side validation
        public static class TestConstants
        {
            public const string TestCubeName = "ManageGO_TestCube";
            public const string TestSphereName = "ManageGO_TestSphere";
            public const string ParentObjectName = "ManageGO_Parent";
            public const string ChildObjectName = "ManageGO_Child";
            public const string PrefabInstanceName = "ManageGO_PrefabInstance";
            public const string TaggedObjectName = "ManageGO_TaggedObject";
            public const string ComponentTestObjectName = "ManageGO_ComponentTest";
            
            public const string TestTag = "ManageGOTestTag";
            public const string TestLayerName = "ManageGOTestLayer";
            public const int TestLayerIndex = 31; // Use last available layer
            
            public const string LogPrefix = "ManageGameObjectIntegrationTest:";
            public const string SetupCompleteMessage = "ManageGameObjectIntegrationTest setup completed successfully";
        }

        /// <summary>
        /// Sets up the integration test environment with various test GameObjects.
        /// </summary>
        public override IEnumerator SetupIntegrationTest()
        {
            Debug.Log($"{TestConstants.LogPrefix} Starting setup...");

            // Create test tag if it doesn't exist
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            
            bool tagExists = false;
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == TestConstants.TestTag)
                {
                    tagExists = true;
                    break;
                }
            }
            
            if (!tagExists)
            {
                tagsProp.InsertArrayElementAtIndex(0);
                tagsProp.GetArrayElementAtIndex(0).stringValue = TestConstants.TestTag;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"{TestConstants.LogPrefix} Created test tag: {TestConstants.TestTag}");
            }

            // Set up test layer
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (layersProp.GetArrayElementAtIndex(TestConstants.TestLayerIndex).stringValue != TestConstants.TestLayerName)
            {
                layersProp.GetArrayElementAtIndex(TestConstants.TestLayerIndex).stringValue = TestConstants.TestLayerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"{TestConstants.LogPrefix} Set up test layer: {TestConstants.TestLayerName} at index {TestConstants.TestLayerIndex}");
            }

            yield return null;

            // Create basic test objects
            testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testCube.name = TestConstants.TestCubeName;
            testCube.transform.position = new Vector3(0, 0, 0);
            Debug.Log($"{TestConstants.LogPrefix} Created test cube: {testCube.name}");

            testSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            testSphere.name = TestConstants.TestSphereName;
            testSphere.transform.position = new Vector3(2, 0, 0);
            Debug.Log($"{TestConstants.LogPrefix} Created test sphere: {testSphere.name}");

            yield return null;

            // Create parent-child hierarchy
            parentObject = new GameObject(TestConstants.ParentObjectName);
            parentObject.transform.position = new Vector3(0, 2, 0);
            
            childObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            childObject.name = TestConstants.ChildObjectName;
            childObject.transform.parent = parentObject.transform;
            childObject.transform.localPosition = Vector3.zero;
            Debug.Log($"{TestConstants.LogPrefix} Created parent-child hierarchy");

            yield return null;

            // Create tagged object
            taggedObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            taggedObject.name = TestConstants.TaggedObjectName;
            taggedObject.tag = TestConstants.TestTag;
            taggedObject.layer = TestConstants.TestLayerIndex;
            taggedObject.transform.position = new Vector3(-2, 0, 0);
            Debug.Log($"{TestConstants.LogPrefix} Created tagged object with tag: {TestConstants.TestTag} and layer: {TestConstants.TestLayerIndex}");

            // Create object with components
            componentTestObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            componentTestObject.name = TestConstants.ComponentTestObjectName;
            componentTestObject.transform.position = new Vector3(0, -2, 0);
            
            // Add Rigidbody component
            var rigidbody = componentTestObject.AddComponent<Rigidbody>();
            rigidbody.mass = 2f;
            rigidbody.useGravity = false;
            
            // Add BoxCollider (Plane already has MeshCollider, add another collider)
            var boxCollider = componentTestObject.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(2, 0.1f, 2);
            boxCollider.isTrigger = true;
            
            Debug.Log($"{TestConstants.LogPrefix} Created component test object with Rigidbody and BoxCollider");

            yield return null;

            // Try to create a prefab instance (use a simple prefab if available)
            // For now, we'll just create another tagged object to simulate prefab instance
            prefabInstance = GameObject.CreatePrimitive(PrimitiveType.Quad);
            prefabInstance.name = TestConstants.PrefabInstanceName;
            prefabInstance.transform.position = new Vector3(0, 0, 2);
            Debug.Log($"{TestConstants.LogPrefix} Created prefab instance placeholder");

            // Log object hierarchy
            LogSceneHierarchy();

            // Final setup complete message
            Debug.Log(TestConstants.SetupCompleteMessage);
            
            yield return null;
        }

        /// <summary>
        /// Cleans up all test GameObjects and resets the test environment.
        /// </summary>
        public override IEnumerator CleanupIntegrationTest()
        {
            Debug.Log($"{TestConstants.LogPrefix} Starting cleanup...");

            // Destroy all test objects
            DestroyTestObject(ref testCube, "test cube");
            DestroyTestObject(ref testSphere, "test sphere");
            DestroyTestObject(ref parentObject, "parent object (and children)");
            DestroyTestObject(ref taggedObject, "tagged object");
            DestroyTestObject(ref componentTestObject, "component test object");
            DestroyTestObject(ref prefabInstance, "prefab instance");

            yield return null;

            // Clean up any orphaned test objects by name pattern
            GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj != null && obj.name.StartsWith("ManageGO_"))
                {
                    Debug.Log($"{TestConstants.LogPrefix} Cleaning up orphaned object: {obj.name}");
                    Object.DestroyImmediate(obj);
                }
            }

            yield return null;

            Debug.Log($"{TestConstants.LogPrefix} Cleanup completed");
        }

        /// <summary>
        /// Helper method to destroy a test GameObject safely.
        /// </summary>
        private void DestroyTestObject(ref GameObject obj, string description)
        {
            if (obj != null)
            {
                Object.DestroyImmediate(obj);
                obj = null;
                Debug.Log($"{TestConstants.LogPrefix} Destroyed {description}");
            }
        }

        /// <summary>
        /// Logs the current scene hierarchy for debugging.
        /// </summary>
        private void LogSceneHierarchy()
        {
            Debug.Log($"{TestConstants.LogPrefix} Current scene hierarchy:");
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                if (root.name.StartsWith("ManageGO_"))
                {
                    LogGameObjectHierarchy(root, 0);
                }
            }
        }

        /// <summary>
        /// Recursively logs a GameObject and its children.
        /// </summary>
        private void LogGameObjectHierarchy(GameObject obj, int depth)
        {
            string indent = new string(' ', depth * 2);
            string components = "";
            
            var comps = obj.GetComponents<Component>();
            if (comps.Length > 1) // More than just Transform
            {
                components = " [Components: " + string.Join(", ", System.Array.ConvertAll(comps, c => c.GetType().Name)) + "]";
            }
            
            Debug.Log($"{TestConstants.LogPrefix} {indent}- {obj.name} (Tag: {obj.tag}, Layer: {obj.layer}){components}");
            
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                LogGameObjectHierarchy(obj.transform.GetChild(i).gameObject, depth + 1);
            }
        }
    }
}