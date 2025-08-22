using NUnit.Framework;
using UnityEngine;
using Newtonsoft.Json.Linq;
using UMCP.Editor.Tools.Utilities;

namespace UMCP.Tests.Editor
{
    /// <summary>
    /// Unit tests for PropertyUtility functionality.
    /// Tests property setting operations using reflection in a functional manner.
    /// </summary>
    public class PropertyUtilityTests
    {
        #region Test Helper Classes

        /// <summary>
        /// Test class with various property types for testing property setting functionality.
        /// </summary>
        public class TestComponent : MonoBehaviour
        {
            public string StringProperty { get; set; } = "initial";
            public int IntProperty { get; set; } = 0;
            public float FloatProperty { get; set; } = 0.0f;
            public bool BoolProperty { get; set; } = false;
            public Vector3 VectorProperty { get; set; } = Vector3.zero;
            
            [SerializeField]
            private string _privateField = "private";
            
            public string GetPrivateField() => _privateField;
        }

        #endregion

        [Test]
        [Description("Tests that ApplyObjectProperties successfully sets string properties")]
        public void ApplyObjectProperties_SetsStringProperty_Successfully()
        {
            // Arrange
            var gameObject = new GameObject("TestObject");
            var testObject = gameObject.AddComponent<TestComponent>();
            var properties = new JObject
            {
                ["StringProperty"] = "new value"
            };

            // Act
            bool result = PropertyUtility.ApplyObjectProperties(testObject, properties);

            // Assert
            Assert.That(result, Is.True, "Should return true when property is successfully set");
            Assert.That(testObject.StringProperty, Is.EqualTo("new value"), "Should update string property value");
            
            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        [Description("Tests that ApplyObjectProperties successfully sets numeric properties")]
        public void ApplyObjectProperties_SetsNumericProperties_Successfully()
        {
            // Arrange
            var gameObject = new GameObject("TestObject");
            var testObject = gameObject.AddComponent<TestComponent>();
            var properties = new JObject
            {
                ["IntProperty"] = 42,
                ["FloatProperty"] = 3.14f,
                ["BoolProperty"] = true
            };

            // Act
            bool result = PropertyUtility.ApplyObjectProperties(testObject, properties);

            // Assert
            Assert.That(result, Is.True, "Should return true when properties are successfully set");
            Assert.That(testObject.IntProperty, Is.EqualTo(42), "Should update int property value");
            Assert.That(testObject.FloatProperty, Is.EqualTo(3.14f).Within(0.001f), "Should update float property value");
            Assert.That(testObject.BoolProperty, Is.True, "Should update bool property value");
            
            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        [Description("Tests that ApplyObjectProperties successfully sets Vector3 properties")]
        public void ApplyObjectProperties_SetsVectorProperty_Successfully()
        {
            // Arrange
            var gameObject = new GameObject("TestObject");
            var testObject = gameObject.AddComponent<TestComponent>();
            var properties = new JObject
            {
                ["VectorProperty"] = new JArray { 1.0f, 2.0f, 3.0f }
            };

            // Act
            bool result = PropertyUtility.ApplyObjectProperties(testObject, properties);

            // Assert
            Assert.That(result, Is.True, "Should return true when property is successfully set");
            Assert.That(testObject.VectorProperty, Is.EqualTo(new Vector3(1.0f, 2.0f, 3.0f)), "Should update Vector3 property value");
            
            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        [Description("Tests that ApplyObjectProperties handles null parameters gracefully")]
        public void ApplyObjectProperties_HandlesNullParameters_Gracefully()
        {
            // Test null target
            bool resultNullTarget = PropertyUtility.ApplyObjectProperties(null, new JObject());
            Assert.That(resultNullTarget, Is.False, "Should return false for null target");

            // Test null properties
            var gameObject = new GameObject("TestObject");
            var testObject = gameObject.AddComponent<TestComponent>();
            bool resultNullProps = PropertyUtility.ApplyObjectProperties(testObject, null);
            Assert.That(resultNullProps, Is.False, "Should return false for null properties");
            
            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        [Description("Tests that ApplyObjectProperties returns false when no properties are modified")]
        public void ApplyObjectProperties_ReturnsFalse_WhenNoPropertiesModified()
        {
            // Arrange
            var gameObject = new GameObject("TestObject");
            var testObject = gameObject.AddComponent<TestComponent>();
            testObject.StringProperty = "existing value";
            
            var properties = new JObject
            {
                ["StringProperty"] = "existing value", // Same value, no change
                ["NonExistentProperty"] = "value"      // Non-existent property
            };

            // Act
            bool result = PropertyUtility.ApplyObjectProperties(testObject, properties);

            // Assert
            Assert.That(result, Is.False, "Should return false when no properties are actually modified");
            
            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        [Description("Tests that SetPropertyOrField handles property setting correctly")]
        public void SetPropertyOrField_SetsProperty_Successfully()
        {
            // Arrange
            var gameObject = new GameObject("TestObject");
            var testObject = gameObject.AddComponent<TestComponent>();
            var value = JToken.FromObject("test value");

            // Act
            bool result = PropertyUtility.SetPropertyOrField(testObject, "StringProperty", value);

            // Assert
            Assert.That(result, Is.True, "Should return true when property is successfully set");
            Assert.That(testObject.StringProperty, Is.EqualTo("test value"), "Should set property value correctly");
            
            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        [Description("Tests that SetPropertyOrField handles non-existent properties gracefully")]
        public void SetPropertyOrField_HandlesNonExistentProperty_Gracefully()
        {
            // Arrange
            var gameObject = new GameObject("TestObject");
            var testObject = gameObject.AddComponent<TestComponent>();
            var value = JToken.FromObject("test value");

            // Act
            bool result = PropertyUtility.SetPropertyOrField(testObject, "NonExistentProperty", value);

            // Assert
            Assert.That(result, Is.False, "Should return false for non-existent property");
            
            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        [Description("Tests that SetPropertyOrField handles case-insensitive property names")]
        public void SetPropertyOrField_HandlesCaseInsensitive_PropertyNames()
        {
            // Arrange
            var gameObject = new GameObject("TestObject");
            var testObject = gameObject.AddComponent<TestComponent>();
            var value = JToken.FromObject("test value");

            // Act
            bool result = PropertyUtility.SetPropertyOrField(testObject, "stringproperty", value); // lowercase

            // Assert
            Assert.That(result, Is.True, "Should return true for case-insensitive property match");
            Assert.That(testObject.StringProperty, Is.EqualTo("test value"), "Should set property value correctly despite case difference");
            
            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        [Description("Tests that SetPropertyOrField returns false when setting same value")]
        public void SetPropertyOrField_ReturnsFalse_WhenSettingSameValue()
        {
            // Arrange
            var gameObject = new GameObject("TestObject");
            var testObject = gameObject.AddComponent<TestComponent>();
            testObject.StringProperty = "existing value";
            var value = JToken.FromObject("existing value");

            // Act
            bool result = PropertyUtility.SetPropertyOrField(testObject, "StringProperty", value);

            // Assert
            Assert.That(result, Is.False, "Should return false when setting the same value");
            
            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        [Description("Tests that SetPropertyOrField handles multiple property types in sequence")]
        public void SetPropertyOrField_HandlesMultipleTypes_InSequence()
        {
            // Arrange
            var gameObject = new GameObject("TestObject");
            var testObject = gameObject.AddComponent<TestComponent>();
            
            // Act & Assert
            bool stringResult = PropertyUtility.SetPropertyOrField(testObject, "StringProperty", JToken.FromObject("string value"));
            Assert.That(stringResult, Is.True, "Should successfully set string property");
            Assert.That(testObject.StringProperty, Is.EqualTo("string value"));

            bool intResult = PropertyUtility.SetPropertyOrField(testObject, "IntProperty", JToken.FromObject(123));
            Assert.That(intResult, Is.True, "Should successfully set int property");
            Assert.That(testObject.IntProperty, Is.EqualTo(123));

            bool floatResult = PropertyUtility.SetPropertyOrField(testObject, "FloatProperty", JToken.FromObject(456.789f));
            Assert.That(floatResult, Is.True, "Should successfully set float property");
            Assert.That(testObject.FloatProperty, Is.EqualTo(456.789f).Within(0.001f));

            bool boolResult = PropertyUtility.SetPropertyOrField(testObject, "BoolProperty", JToken.FromObject(true));
            Assert.That(boolResult, Is.True, "Should successfully set bool property");
            Assert.That(testObject.BoolProperty, Is.True);
            
            // Cleanup
            Object.DestroyImmediate(gameObject);
        }
    }
}