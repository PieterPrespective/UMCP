using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using UMCP.Editor.Tools.Utilities;
using UMCP.Editor.Helpers;

namespace UMCP.Tests.Editor
{
    /// <summary>
    /// Unit tests for ManageGameObjectUtility functionality.
    /// Tests GameObject management operations in a functional manner.
    /// </summary>
    public class ManageGameObjectUtilityTests
    {
        private Scene _testScene;
        private List<GameObject> _createdObjects;

        [SetUp]
        public void SetUp()
        {
            // Create a temporary scene for testing
            //_testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            //EditorSceneManager.SetActiveScene(_testScene);
            
            _createdObjects = new List<GameObject>();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up created objects
            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }
            _createdObjects.Clear();

            // Close the test scene
            if (_testScene.IsValid())
            {
                EditorSceneManager.CloseScene(_testScene, true);
            }
        }

        #region Action Validation Tests

        [Test]
        [Description("Tests that IsValidAction correctly identifies valid actions")]
        public void IsValidAction_ReturnsTrueForValidActions()
        {
            // Arrange
            string[] validActions = { "create", "modify", "delete", "find", "get_components", "add_component", "remove_component", "set_component_property" };

            // Act & Assert
            foreach (string action in validActions)
            {
                bool result = ManageGameObjectUtility.IsValidAction(action);
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
                bool result = ManageGameObjectUtility.IsValidAction(action);
                Assert.That(result, Is.False, $"Should return false for invalid action: {action ?? "null"}");
            }
        }

        #endregion

        #region GameObject Creation Tests

        [Test]
        [Description("Tests that CreateGameObject successfully creates an empty GameObject")]
        public void CreateGameObject_CreatesEmptyGameObject_Successfully()
        {
            // Arrange
            var creationData = new GameObjectCreationData
            {
                Name = "TestGameObject",
                ComponentsToAdd = new List<string>()
            };

            // Act
            var result = ManageGameObjectUtility.CreateGameObject(creationData);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Use JsonSerializerSettings to handle circular references in Vector3
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            
            var resultJson = JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(settings));
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            // Find the created object
            GameObject createdObj = GameObject.Find("TestGameObject");
            Assert.That(createdObj, Is.Not.Null, "Should create the GameObject in the scene");
            _createdObjects.Add(createdObj);
        }

        [Test]
        [Description("Tests that CreateGameObject creates a primitive GameObject")]
        public void CreateGameObject_CreatesPrimitive_Successfully()
        {
            // Arrange
            var creationData = new GameObjectCreationData
            {
                Name = "TestCube",
                PrimitiveType = "Cube",
                ComponentsToAdd = new List<string>()
            };

            // Act
            var result = ManageGameObjectUtility.CreateGameObject(creationData);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Use JsonSerializerSettings to handle circular references in Vector3
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            
            var resultJson = JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(settings));
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            // Find the created object and verify it has cube components
            GameObject createdObj = GameObject.Find("TestCube");
            Assert.That(createdObj, Is.Not.Null, "Should create the primitive GameObject");
            
            var meshRenderer = createdObj.GetComponent<MeshRenderer>();
            Assert.That(meshRenderer, Is.Not.Null, "Cube primitive should have MeshRenderer");
            
            var boxCollider = createdObj.GetComponent<BoxCollider>();
            Assert.That(boxCollider, Is.Not.Null, "Cube primitive should have BoxCollider");
            
            _createdObjects.Add(createdObj);
        }

        [Test]
        [Description("Tests that CreateGameObject sets tag and layer correctly")]
        public void CreateGameObject_SetsTagAndLayer_Correctly()
        {
            // Arrange
            var creationData = new GameObjectCreationData
            {
                Name = "TestTaggedObject",
                Tag = "Player",
                Layer = 1, // Layer 1 is usually "TransparentFX"
                ComponentsToAdd = new List<string>()
            };

            // Act
            var result = ManageGameObjectUtility.CreateGameObject(creationData);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Use JsonSerializerSettings to handle circular references in Vector3
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            
            var resultJson = JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(settings));
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            // Find the created object and verify tag and layer
            GameObject createdObj = GameObject.Find("TestTaggedObject");
            Assert.That(createdObj, Is.Not.Null, "Should create the GameObject");
            Assert.That(createdObj.tag, Is.EqualTo("Player"), "Should set the correct tag");
            Assert.That(createdObj.layer, Is.EqualTo(1), "Should set the correct layer");
            
            _createdObjects.Add(createdObj);
        }

        [Test]
        [Description("Tests that CreateGameObject returns error for missing name")]
        public void CreateGameObject_ReturnsError_ForMissingName()
        {
            // Arrange
            var creationData = new GameObjectCreationData
            {
                Name = null,
                ComponentsToAdd = new List<string>()
            };

            // Act
            var result = ManageGameObjectUtility.CreateGameObject(creationData);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Use JsonSerializerSettings to handle circular references
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            
            var resultJson = JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(settings));
            bool success = resultJson["success"]?.ToObject<bool>() ?? true;
            
            Assert.That(success, Is.False, "Should return error status for missing name");
        }

        #endregion

        #region GameObject Finding Tests

        [Test]
        [Description("Tests that FindGameObject finds GameObject by name")]
        public void FindGameObject_FindsByName_Successfully()
        {
            // Arrange
            var testObj = new GameObject("FindTestObject");
            _createdObjects.Add(testObj);
            
            var nameToken = JToken.FromObject("FindTestObject");

            // Act
            var result = ManageGameObjectUtility.FindGameObject(nameToken, "name");

            // Assert
            Assert.That(result, Is.Not.Null, "Should find the GameObject by name");
            Assert.That(result.name, Is.EqualTo("FindTestObject"), "Should return the correct GameObject");
        }

        [Test]
        [Description("Tests that FindGameObject finds GameObject by instance ID")]
        public void FindGameObject_FindsByInstanceID_Successfully()
        {
            // Arrange
            var testObj = new GameObject("InstanceIDTestObject");
            _createdObjects.Add(testObj);
            
            int instanceId = testObj.GetInstanceID();
            var idToken = JToken.FromObject(instanceId);

            // Act
            var result = ManageGameObjectUtility.FindGameObject(idToken, "auto");

            // Assert
            Assert.That(result, Is.Not.Null, "Should find the GameObject by instance ID");
            Assert.That(result.GetInstanceID(), Is.EqualTo(instanceId), "Should return the correct GameObject");
        }

        [Test]
        [Description("Tests that FindGameObject returns null for non-existent object")]
        public void FindGameObject_ReturnsNull_ForNonExistentObject()
        {
            // Arrange
            var nameToken = JToken.FromObject("NonExistentObject");

            // Act
            var result = ManageGameObjectUtility.FindGameObject(nameToken, "name");

            // Assert
            Assert.That(result, Is.Null, "Should return null for non-existent GameObject");
        }

        [Test]
        [Description("Tests that FindGameObjects finds multiple objects by tag")]
        public void FindGameObjects_FindsByTag_Successfully()
        {
            // Arrange
            var obj1 = new GameObject("TaggedObject1");
            var obj2 = new GameObject("TaggedObject2");
            obj1.tag = "Player";
            obj2.tag = "Player";
            _createdObjects.Add(obj1);
            _createdObjects.Add(obj2);

            var searchData = new GameObjectSearchData
            {
                Tag = "Player",
                MaxResults = 100
            };

            // Act
            var results = ManageGameObjectUtility.FindGameObjects(searchData);

            // Assert
            Assert.That(results.Count, Is.GreaterThanOrEqualTo(2), "Should find at least 2 GameObjects with Player tag");
            Assert.That(results.Any(go => go.name == "TaggedObject1"), Is.True, "Should include TaggedObject1");
            Assert.That(results.Any(go => go.name == "TaggedObject2"), Is.True, "Should include TaggedObject2");
        }

        #endregion

        #region GameObject Deletion Tests

        [Test]
        [Description("Tests that DeleteGameObject successfully removes GameObject")]
        public void DeleteGameObject_RemovesGameObject_Successfully()
        {
            // Arrange
            var testObj = new GameObject("DeleteTestObject");
            _createdObjects.Add(testObj);
            
            // Verify it exists
            Assert.That(GameObject.Find("DeleteTestObject"), Is.Not.Null, "Test object should exist before deletion");

            // Act
            var result = ManageGameObjectUtility.DeleteGameObject(testObj);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Use JsonSerializerSettings to handle circular references in Vector3
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            
            var resultJson = JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(settings));
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            Assert.That(GameObject.Find("DeleteTestObject"), Is.Null, "GameObject should be deleted");
            
            // Remove from cleanup list since it's already destroyed
            _createdObjects.Remove(testObj);
        }

        [Test]
        [Description("Tests that DeleteGameObject returns error for null GameObject")]
        public void DeleteGameObject_ReturnsError_ForNullGameObject()
        {
            // Act
            var result = ManageGameObjectUtility.DeleteGameObject(null);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Use JsonSerializerSettings to handle circular references
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            
            var resultJson = JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(settings));
            bool success = resultJson["success"]?.ToObject<bool>() ?? true;
            
            Assert.That(success, Is.False, "Should return error status for null GameObject");
        }

        #endregion

        #region Component Management Tests

        [Test]
        [Description("Tests that AddComponent successfully adds a component")]
        public void AddComponent_AddsComponent_Successfully()
        {
            // Arrange
            var testObj = new GameObject("ComponentTestObject");
            _createdObjects.Add(testObj);

            // Act
            var result = ManageGameObjectUtility.AddComponent(testObj, "Rigidbody");

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Use JsonSerializerSettings to handle circular references in Vector3
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            
            var resultJson = JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(settings));
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            var rigidbody = testObj.GetComponent<Rigidbody>();
            Assert.That(rigidbody, Is.Not.Null, "Should add Rigidbody component");
        }

        [Test]
        [Description("Tests that AddComponent returns error for invalid component type")]
        public void AddComponent_ReturnsError_ForInvalidComponentType()
        {
            // Arrange
            var testObj = new GameObject("ComponentTestObject");
            _createdObjects.Add(testObj);

            // Act
            var result = ManageGameObjectUtility.AddComponent(testObj, "InvalidComponent");

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Use JsonSerializerSettings to handle circular references
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            
            var resultJson = JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(settings));
            bool success = resultJson["success"]?.ToObject<bool>() ?? true;
            
            Assert.That(success, Is.False, "Should return error status for invalid component type");
        }

        [Test]
        [Description("Tests that RemoveComponent successfully removes a component")]
        public void RemoveComponent_RemovesComponent_Successfully()
        {
            // Arrange
            var testObj = new GameObject("ComponentTestObject");
            var rigidbody = testObj.AddComponent<Rigidbody>();
            _createdObjects.Add(testObj);
            
            // Verify component exists
            Assert.That(testObj.GetComponent<Rigidbody>(), Is.Not.Null, "Rigidbody should exist before removal");

            // Act
            var result = ManageGameObjectUtility.RemoveComponent(testObj, "Rigidbody");

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Use JsonSerializerSettings to handle circular references in Vector3
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            
            var resultJson = JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(settings));
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            Assert.That(testObj.GetComponent<Rigidbody>(), Is.Null, "Rigidbody component should be removed");
        }

        [Test]
        [Description("Tests that RemoveComponent prevents removing Transform component")]
        public void RemoveComponent_PreventsRemovingTransform()
        {
            // Arrange
            var testObj = new GameObject("ComponentTestObject");
            _createdObjects.Add(testObj);

            // Act
            var result = ManageGameObjectUtility.RemoveComponent(testObj, "Transform");

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Use JsonSerializerSettings to handle circular references
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            
            var resultJson = JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(settings));
            bool success = resultJson["success"]?.ToObject<bool>() ?? true;
            
            Assert.That(success, Is.False, "Should return error status when trying to remove Transform");
            Assert.That(testObj.GetComponent<Transform>(), Is.Not.Null, "Transform component should still exist");
        }

        [Test]
        [Description("Tests that GetComponents returns all components on GameObject")]
        public void GetComponents_ReturnsAllComponents_Successfully()
        {
            // Arrange
            var testObj = new GameObject("ComponentTestObject");
            testObj.AddComponent<Rigidbody>();
            testObj.AddComponent<BoxCollider>();
            _createdObjects.Add(testObj);

            // Act
            var result = ManageGameObjectUtility.GetComponents(testObj);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Use JsonSerializerSettings to handle circular references in Vector3
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            
            var resultJson = JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(settings));
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            
            var componentsList = resultJson["data"] as JArray;
            Assert.That(componentsList, Is.Not.Null, "Should include components list");
            Assert.That(componentsList.Count, Is.GreaterThanOrEqualTo(3), "Should have at least Transform, Rigidbody, and BoxCollider");
        }

        [Test]
        [Description("Tests that SetComponentProperty modifies component properties")]
        public void SetComponentProperty_ModifiesProperties_Successfully()
        {
            // Arrange
            var testObj = new GameObject("ComponentTestObject");
            var rigidbody = testObj.AddComponent<Rigidbody>();
            _createdObjects.Add(testObj);
            
            var componentData = new ComponentPropertyData
            {
                ComponentTypeName = "Rigidbody",
                Properties = new JObject
                {
                    ["mass"] = 5.0f,
                    ["drag"] = 0.5f
                }
            };

            // Act
            var result = ManageGameObjectUtility.SetComponentProperty(testObj, componentData);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");
            
            // Use JsonSerializerSettings to handle circular references in Vector3
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            
            var resultJson = JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(settings));
            bool success = resultJson["success"]?.ToObject<bool>() ?? false;
            
            Assert.That(success, Is.True, "Should return success response");
            Assert.That(rigidbody.mass, Is.EqualTo(5.0f).Within(0.001f), "Should set mass property");
            Assert.That(rigidbody.linearDamping, Is.EqualTo(0.5f).Within(0.001f), "Should set drag property");
        }

        #endregion

        #region Data Structure Tests

        [Test]
        [Description("Tests that TransformData handles Vector3 values correctly")]
        public void TransformData_HandlesVector3Values_Correctly()
        {
            // Arrange & Act
            var transformData = new TransformData
            {
                Position = new Vector3(1, 2, 3),
                Rotation = Quaternion.identity,
                Scale = new Vector3(2, 2, 2)
            };

            // Assert
            Assert.That(transformData.Position, Is.EqualTo(new Vector3(1, 2, 3)), "Should store position correctly");
            Assert.That(transformData.Rotation, Is.EqualTo(Quaternion.identity), "Should store rotation correctly");
            Assert.That(transformData.Scale, Is.EqualTo(new Vector3(2, 2, 2)), "Should store scale correctly");
        }

        [Test]
        [Description("Tests that GameObjectSearchData handles search parameters correctly")]
        public void GameObjectSearchData_HandlesSearchParameters_Correctly()
        {
            // Arrange & Act
            var searchData = new GameObjectSearchData
            {
                Name = "TestObject",
                Tag = "Player",
                Layer = 5,
                ComponentType = "Rigidbody",
                MaxResults = 50
            };

            // Assert
            Assert.That(searchData.Name, Is.EqualTo("TestObject"), "Should store name correctly");
            Assert.That(searchData.Tag, Is.EqualTo("Player"), "Should store tag correctly");
            Assert.That(searchData.Layer, Is.EqualTo(5), "Should store layer correctly");
            Assert.That(searchData.ComponentType, Is.EqualTo("Rigidbody"), "Should store component type correctly");
            Assert.That(searchData.MaxResults, Is.EqualTo(50), "Should store max results correctly");
        }

        #endregion
    }
}