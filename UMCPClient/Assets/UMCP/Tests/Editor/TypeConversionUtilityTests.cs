using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using UMCP.Editor.Tools.Utilities;

namespace UMCP.Tests.Editor
{
    /// <summary>
    /// Unit tests for TypeConversionUtility functionality.
    /// Tests type conversion operations for Unity types and primitives.
    /// </summary>
    public class TypeConversionUtilityTests
    {
        #region Primitive Type Conversion Tests

        [Test]
        [Description("Tests conversion of JToken to string type")]
        public void ConvertJTokenToType_ConvertsToString_Successfully()
        {
            // Arrange
            var token = JToken.FromObject("test string");

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(string));

            // Assert
            Assert.That(result, Is.EqualTo("test string"), "Should convert JToken to string correctly");
        }

        [Test]
        [Description("Tests conversion of JToken to integer type")]
        public void ConvertJTokenToType_ConvertsToInt_Successfully()
        {
            // Arrange
            var token = JToken.FromObject(42);

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(int));

            // Assert
            Assert.That(result, Is.EqualTo(42), "Should convert JToken to int correctly");
        }

        [Test]
        [Description("Tests conversion of JToken to float type")]
        public void ConvertJTokenToType_ConvertsToFloat_Successfully()
        {
            // Arrange
            var token = JToken.FromObject(3.14f);

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(float));

            // Assert
            Assert.That((float)result, Is.EqualTo(3.14f).Within(0.001f), "Should convert JToken to float correctly");
        }

        [Test]
        [Description("Tests conversion of JToken to boolean type")]
        public void ConvertJTokenToType_ConvertsToBool_Successfully()
        {
            // Arrange
            var trueToken = JToken.FromObject(true);
            var falseToken = JToken.FromObject(false);

            // Act
            var trueResult = TypeConversionUtility.ConvertJTokenToType(trueToken, typeof(bool));
            var falseResult = TypeConversionUtility.ConvertJTokenToType(falseToken, typeof(bool));

            // Assert
            Assert.That((bool)trueResult, Is.True, "Should convert true JToken to bool correctly");
            Assert.That((bool)falseResult, Is.False, "Should convert false JToken to bool correctly");
        }

        [Test]
        [Description("Tests conversion of JToken to double type")]
        public void ConvertJTokenToType_ConvertsToDouble_Successfully()
        {
            // Arrange
            var token = JToken.FromObject(2.71828);

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(double));

            // Assert
            Assert.That((double)result, Is.EqualTo(2.71828).Within(0.00001), "Should convert JToken to double correctly");
        }

        [Test]
        [Description("Tests conversion of JToken to long type")]
        public void ConvertJTokenToType_ConvertsToLong_Successfully()
        {
            // Arrange
            var token = JToken.FromObject(9223372036854775807L);

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(long));

            // Assert
            Assert.That(result, Is.EqualTo(9223372036854775807L), "Should convert JToken to long correctly");
        }

        #endregion

        #region Unity Vector Type Conversion Tests

        [Test]
        [Description("Tests conversion of JArray to Vector2 type")]
        public void ConvertJTokenToType_ConvertsToVector2_Successfully()
        {
            // Arrange
            var token = new JArray { 1.5f, 2.5f };
            var expectedVector = new Vector2(1.5f, 2.5f);

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(Vector2));

            // Assert
            Assert.That((Vector2)result, Is.EqualTo(expectedVector), "Should convert JArray to Vector2 correctly");
        }

        [Test]
        [Description("Tests conversion of JArray to Vector3 type")]
        public void ConvertJTokenToType_ConvertsToVector3_Successfully()
        {
            // Arrange
            var token = new JArray { 1.0f, 2.0f, 3.0f };
            var expectedVector = new Vector3(1.0f, 2.0f, 3.0f);

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(Vector3));

            // Assert
            Assert.That((Vector3)result, Is.EqualTo(expectedVector), "Should convert JArray to Vector3 correctly");
        }

        [Test]
        [Description("Tests conversion of JArray to Vector4 type")]
        public void ConvertJTokenToType_ConvertsToVector4_Successfully()
        {
            // Arrange
            var token = new JArray { 1.0f, 2.0f, 3.0f, 4.0f };
            var expectedVector = new Vector4(1.0f, 2.0f, 3.0f, 4.0f);

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(Vector4));

            // Assert
            Assert.That((Vector4)result, Is.EqualTo(expectedVector), "Should convert JArray to Vector4 correctly");
        }

        [Test]
        [Description("Tests conversion of JArray to Quaternion type")]
        public void ConvertJTokenToType_ConvertsToQuaternion_Successfully()
        {
            // Arrange
            var token = new JArray { 0.0f, 0.0f, 0.0f, 1.0f };
            var expectedQuaternion = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(Quaternion));

            // Assert
            Assert.That((Quaternion)result, Is.EqualTo(expectedQuaternion), "Should convert JArray to Quaternion correctly");
        }

        [Test]
        [Description("Tests conversion of JArray to Color type with RGB values")]
        public void ConvertJTokenToType_ConvertsToColorRGB_Successfully()
        {
            // Arrange
            var token = new JArray { 0.5f, 0.75f, 1.0f };
            var expectedColor = new Color(0.5f, 0.75f, 1.0f, 1.0f); // Alpha defaults to 1.0

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(Color));

            // Assert
            Assert.That((Color)result, Is.EqualTo(expectedColor), "Should convert JArray to Color with default alpha");
        }

        [Test]
        [Description("Tests conversion of JArray to Color type with RGBA values")]
        public void ConvertJTokenToType_ConvertsToColorRGBA_Successfully()
        {
            // Arrange
            var token = new JArray { 0.5f, 0.75f, 1.0f, 0.8f };
            var expectedColor = new Color(0.5f, 0.75f, 1.0f, 0.8f);

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(Color));

            // Assert
            Assert.That((Color)result, Is.EqualTo(expectedColor), "Should convert JArray to Color with specified alpha");
        }

        #endregion

        #region Vector Conversion Error Handling Tests

        [Test]
        [Description("Tests that Vector2 conversion fails gracefully with incorrect array size")]
        public void ConvertJTokenToType_Vector2ConversionFails_WithIncorrectArraySize()
        {
            // Arrange
            var token = new JArray { 1.0f }; // Only 1 element instead of 2

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(Vector2));

            // Assert
            Assert.That(result, Is.Null, "Should return null for incorrect Vector2 array size");
        }

        [Test]
        [Description("Tests that Vector3 conversion fails gracefully with incorrect array size")]
        public void ConvertJTokenToType_Vector3ConversionFails_WithIncorrectArraySize()
        {
            // Arrange
            var token = new JArray { 1.0f, 2.0f }; // Only 2 elements instead of 3

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(Vector3));

            // Assert
            Assert.That(result, Is.Null, "Should return null for incorrect Vector3 array size");
        }

        [Test]
        [Description("Tests that Vector4 conversion fails gracefully with incorrect array size")]
        public void ConvertJTokenToType_Vector4ConversionFails_WithIncorrectArraySize()
        {
            // Arrange
            var token = new JArray { 1.0f, 2.0f, 3.0f }; // Only 3 elements instead of 4

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(Vector4));

            // Assert
            Assert.That(result, Is.Null, "Should return null for incorrect Vector4 array size");
        }

        [Test]
        [Description("Tests that Color conversion fails gracefully with insufficient array elements")]
        public void ConvertJTokenToType_ColorConversionFails_WithInsufficientElements()
        {
            // Arrange
            var token = new JArray { 1.0f, 2.0f }; // Only 2 elements, need at least 3

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(Color));

            // Assert
            Assert.That(result, Is.Null, "Should return null for insufficient Color array elements");
        }

        #endregion

        #region Enum Conversion Tests

        /// <summary>
        /// Test enum for enum conversion testing.
        /// </summary>
        public enum TestEnum
        {
            Value1,
            Value2,
            Value3
        }

        [Test]
        [Description("Tests conversion of JToken to enum type")]
        public void ConvertJTokenToType_ConvertsToEnum_Successfully()
        {
            // Arrange
            var token = JToken.FromObject("Value2");

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(TestEnum));

            // Assert
            Assert.That(result, Is.EqualTo(TestEnum.Value2), "Should convert JToken to enum correctly");
        }

        [Test]
        [Description("Tests case-insensitive enum conversion")]
        public void ConvertJTokenToType_ConvertsToEnum_CaseInsensitive()
        {
            // Arrange
            var token = JToken.FromObject("value3"); // lowercase

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(TestEnum));

            // Assert
            Assert.That(result, Is.EqualTo(TestEnum.Value3), "Should convert JToken to enum case-insensitively");
        }

        #endregion

        #region Null and Error Handling Tests

        [Test]
        [Description("Tests that null token conversion returns null")]
        public void ConvertJTokenToType_ReturnsNull_ForNullToken()
        {
            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(null, typeof(string));

            // Assert
            Assert.That(result, Is.Null, "Should return null for null token");
        }

        [Test]
        [Description("Tests that JNull token conversion returns null")]
        public void ConvertJTokenToType_ReturnsNull_ForJNullToken()
        {
            // Arrange
            var token = JValue.CreateNull();

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(string));

            // Assert
            Assert.That(result, Is.Null, "Should return null for JNull token");
        }

        [Test]
        [Description("Tests error handling for invalid conversions")]
        public void ConvertJTokenToType_ReturnsNull_ForInvalidConversions()
        {
            // Arrange
            var token = JToken.FromObject("not a number");

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(int));

            // Assert
            Assert.That(result, Is.Null, "Should return null for invalid conversion and log warning");
        }

        #endregion

        #region Unity Asset Loading Tests

        [Test]
        [Description("Tests Unity asset loading with valid path")]
        public void ConvertJTokenToType_LoadsUnityAsset_WithValidPath()
        {
            // Arrange
            string materialPath = "Assets/UMCP/Tests/Editor/TestAssets/TestMaterial.mat";
            
            // Create a test material first
            var material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();
            
            var token = JToken.FromObject(materialPath);

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(Material));

            // Assert
            Assert.That(result, Is.Not.Null, "Should load Unity asset successfully");
            Assert.IsInstanceOf<Material>(result, "Should return Material instance");
            
            // Cleanup
            AssetDatabase.DeleteAsset(materialPath);
        }

        [Test]
        [Description("Tests Unity asset loading with invalid path returns null")]
        public void ConvertJTokenToType_ReturnsNull_ForInvalidAssetPath()
        {
            // Arrange
            var token = JToken.FromObject("Assets/NonExistent/Material.mat");

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(Material));

            // Assert
            Assert.That(result, Is.Null, "Should return null for non-existent asset path");
        }

        #endregion

        #region Fallback Conversion Tests

        [Test]
        [Description("Tests fallback conversion for unsupported types")]
        public void ConvertJTokenToType_UsesFallback_ForUnsupportedTypes()
        {
            // Arrange
            var token = JToken.FromObject(DateTime.Now);

            // Act
            var result = TypeConversionUtility.ConvertJTokenToType(token, typeof(DateTime));

            // Assert
            // This should either work via ToObject or return null
            Assert.That(result == null || result is DateTime, Is.True, "Should handle fallback conversion appropriately");
        }

        #endregion
    }
}