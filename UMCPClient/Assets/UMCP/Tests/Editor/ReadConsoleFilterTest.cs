using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using UMCP.Editor.Tools;
using Newtonsoft.Json.Linq;
using UMCP.Editor.Helpers;

namespace UMCP.Tests.Editor
{
    /// <summary>
    /// Tests for ReadConsole error filtering bug fix.
    /// Verifies that Unity exceptions are correctly classified as errors, not warnings.
    /// </summary>
    [TestFixture]
    public class ReadConsoleFilterTest
    {
        [Test]
        public void TestErrorFilteringDetectsExceptions()
        {
            // Clear console first
            var clearParams = new JObject { ["action"] = "clear" };
            ReadConsole.HandleCommand(clearParams);
            
            // Generate different types of messages
            Debug.Log("Test Log Message");
            Debug.LogWarning("Test Warning Message");
            Debug.LogError("Test Error Message");
            
            // Generate an exception
            try
            {
                throw new System.Exception("Test Exception Message");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            
            // Test filtering for errors only
            var errorParams = new JObject 
            { 
                ["action"] = "get",
                ["types"] = new JArray { "error" },
                ["format"] = "detailed"
            };
            
            var result = ReadConsole.HandleCommand(errorParams);
            Assert.IsNotNull(result, "Result should not be null");
            
            // Cast to dynamic to access properties
            dynamic dynamicResult = result;
            Assert.IsTrue(dynamicResult.success == true, "Should successfully retrieve logs");
            
            var data = dynamicResult.data as List<object>;
            Assert.IsNotNull(data, "Data should not be null");
            
            // Should find at least 2 entries: the error and the exception
            Assert.GreaterOrEqual(data.Count, 2, "Should find at least the error and exception when filtering for errors");
            
            // Verify entries are actually errors
            bool foundError = false;
            bool foundException = false;
            
            foreach (dynamic entry in data)
            {
                Assert.AreEqual("Error", entry.type, "All entries should be of type Error when filtering for errors");
                
                string message = entry.message;
                if (message.Contains("Test Error Message"))
                    foundError = true;
                if (message.Contains("Test Exception Message"))
                    foundException = true;
            }
            
            Assert.IsTrue(foundError, "Should find the error message when filtering for errors");
            Assert.IsTrue(foundException, "Should find the exception when filtering for errors");
        }
        
        [Test]
        public void TestWarningFilteringDoesNotIncludeErrors()
        {
            // Clear console first
            var clearParams = new JObject { ["action"] = "clear" };
            ReadConsole.HandleCommand(clearParams);
            
            // Generate different types of messages
            Debug.Log("Test Log Message");
            Debug.LogWarning("Test Warning Message");
            Debug.LogError("Test Error Message");
            
            // Test filtering for warnings only
            var warningParams = new JObject 
            { 
                ["action"] = "get",
                ["types"] = new JArray { "warning" },
                ["format"] = "detailed"
            };
            
            var result = ReadConsole.HandleCommand(warningParams);
            Assert.IsNotNull(result, "Result should not be null");
            
            // Cast to dynamic to access properties
            dynamic dynamicResult = result;
            Assert.IsTrue(dynamicResult.success == true, "Should successfully retrieve logs");
            
            var data = dynamicResult.data as List<object>;
            Assert.IsNotNull(data, "Data should not be null");
            
            // Verify no errors are included when filtering for warnings
            foreach (dynamic entry in data)
            {
                Assert.AreEqual("Warning", entry.type, "All entries should be of type Warning when filtering for warnings");
                
                string message = entry.message;
                Assert.IsFalse(message.Contains("Test Error Message"), "Error messages should not appear when filtering for warnings only");
            }
        }
    }
}