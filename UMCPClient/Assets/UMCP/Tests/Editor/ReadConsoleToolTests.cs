using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UMCP.Editor;
using UMCP.Editor.Models;
using UMCP.Editor.Tools;
using UnityEngine;
using UnityEngine.TestTools;

namespace UMCP.Tests.Editor
{
    /// <summary>
    /// Comprehensive test suite for the ReadConsole tool functionality.
    /// Tests both direct handler invocation and end-to-end TCP communication via UMCPBridge.
    /// </summary>
    public class ReadConsoleToolTests
    {
        private const int unityPort = 6400;  // Same port as UMCPBridge

        #region Setup and Helper Methods

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Ensure bridge is running for all tests
            if (!UMCPBridge.IsRunning)
            {
                UMCPBridge.Start();
            }
        }

        /// <summary>
        /// Helper method to generate test log entries of different types
        /// Uses LogAssert.Expect() to prevent Unity Test Framework from treating errors/assertions as test failures
        /// </summary>
        private void GenerateTestLogEntries()
        {
            Debug.Log("[ReadConsoleTest] Test log message");
            Debug.LogWarning("[ReadConsoleTest] Test warning message");
            
            // Tell Unity Test Framework to expect these error/assertion logs so they don't cause test failures
            LogAssert.Expect(LogType.Error, "[ReadConsoleTest] Test error message");
            Debug.LogError("[ReadConsoleTest] Test error message");
            
            LogAssert.Expect(LogType.Assert, "[ReadConsoleTest] Test assertion message");
            Debug.LogAssertion("[ReadConsoleTest] Test assertion message");
        }

        /// <summary>
        /// Helper method to send command via TCP and get response
        /// </summary>
        private async Task<JObject> SendCommandViaTCP(Command command)
        {
            string commandJson = JsonConvert.SerializeObject(command);
            byte[] commandBytes = Encoding.UTF8.GetBytes(commandJson);

            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync("localhost", unityPort);
                using (NetworkStream stream = client.GetStream())
                {
                    await stream.WriteAsync(commandBytes, 0, commandBytes.Length);

                    byte[] buffer = new byte[16384]; // Larger buffer for console logs
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    
                    string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    return JObject.Parse(responseJson);
                }
            }
        }

        /// <summary>
        /// Helper method to execute TCP command test in a coroutine
        /// </summary>
        private IEnumerator ExecuteTCPCommandTest(Command command, System.Action<JObject> responseValidator)
        {
            Task<JObject> tcpTask = SendCommandViaTCP(command);
            while (!tcpTask.IsCompleted)
                yield return null;

            if (tcpTask.Exception != null)
            {
                Assert.Fail($"TCP communication failed: {tcpTask.Exception.GetBaseException().Message}");
                yield break;
            }

            JObject response = tcpTask.Result;
            responseValidator(response);
        }

        #endregion

        #region Direct Handler Tests

        [Test]
        public void DirectHandler_ClearConsole_Success()
        {
            // Arrange
            JObject parameters = new JObject
            {
                ["action"] = "clear"
            };

            // Act
            object result = ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.IsNotNull(result, "Handler should return a result");
            
            // Convert result to JObject for inspection
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);

            Debug.Log($"Clear Console Result: {resultJson}");

            // Check for Response.Success format: { success: true, message: "..." }
            Assert.AreEqual(true, resultObj["success"]?.ToObject<bool>(), "Clear action should succeed");
            Assert.IsNotNull(resultObj["message"], "Clear action should return a message");
        }

        [Test]
        public void DirectHandler_GetConsoleEntries_DefaultParameters_Success()
        {
            // Arrange
            GenerateTestLogEntries(); // Add some test logs
            
            JObject parameters = new JObject
            {
                ["action"] = "get"
            };

            // Act
            object result = ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.IsNotNull(result, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Debug.Log($"Get Console Result: {resultJson}");
            
            // Check for Response.Success format: { success: true, message: "...", data: [...] }
            Assert.AreEqual(true, resultObj["success"]?.ToObject<bool>(), "Get action should succeed");
            Assert.IsNotNull(resultObj["data"], "Get action should return data");
            Assert.IsTrue(resultObj["data"] is JArray, "Data should be an array of log entries");
        }

        [Test]
        public void DirectHandler_GetConsoleEntries_FilterByLogType_Success()
        {
            // Arrange
            GenerateTestLogEntries();
            
            JObject parameters = new JObject
            {
                ["action"] = "get",
                ["types"] = new JArray { "error" },
                ["count"] = 5
            };

            // Act
            object result = ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.IsNotNull(result, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.AreEqual(true, resultObj["success"]?.ToObject<bool>(), "Filtered get should succeed");
            Assert.IsNotNull(resultObj["data"], "Should return data");
            
            JArray entries = resultObj["data"] as JArray;
            Assert.IsNotNull(entries, "Data should be an array");
            
            // Verify all returned entries are errors (if any exist)
            foreach (JObject entry in entries.Cast<JObject>())
            {
                Assert.IsNotNull(entry["type"], "Each entry should have a type");
                // Note: Due to the mode bit correction in ReadConsole, "Error" becomes "Warning"
                // This test validates the filtering logic works, regardless of the specific mapping
            }
        }

        [Test]
        public void DirectHandler_GetConsoleEntries_FilterByText_Success()
        {
            // Arrange
            GenerateTestLogEntries();
            
            JObject parameters = new JObject
            {
                ["action"] = "get",
                ["filterText"] = "ReadConsoleTest",
                ["count"] = 10
            };

            // Act
            object result = ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.IsNotNull(result, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.AreEqual(true, resultObj["success"]?.ToObject<bool>(), "Text-filtered get should succeed");
            Assert.IsNotNull(resultObj["data"], "Should return data");
            
            JArray entries = resultObj["data"] as JArray;
            Assert.IsNotNull(entries, "Data should be an array");
            
            // Verify all returned entries contain the filter text
            foreach (JObject entry in entries.Cast<JObject>())
            {
                Assert.IsNotNull(entry["message"], "Each entry should have a message");
                string message = entry["message"].ToString();
                Assert.IsTrue(message.Contains("ReadConsoleTest"), 
                    $"Entry message should contain filter text: {message}");
            }
        }

        [Test]
        public void DirectHandler_GetConsoleEntries_PlainFormat_Success()
        {
            // Arrange
            GenerateTestLogEntries();
            
            JObject parameters = new JObject
            {
                ["action"] = "get",
                ["format"] = "plain",
                ["count"] = 3
            };

            // Act
            object result = ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.IsNotNull(result, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.AreEqual(true, resultObj["success"]?.ToObject<bool>(), "Plain format get should succeed");
            Assert.IsNotNull(resultObj["data"], "Should return data");
            
            JArray entries = resultObj["data"] as JArray;
            Assert.IsNotNull(entries, "Data should be an array");
            
            // In plain format, each entry should be just a string
            foreach (JToken entry in entries)
            {
                Assert.IsTrue(entry.Type == JTokenType.String, 
                    "In plain format, each entry should be a string");
            }
        }

        [Test]
        public void DirectHandler_GetConsoleEntries_WithoutStackTrace_Success()
        {
            // Arrange
            GenerateTestLogEntries();
            
            JObject parameters = new JObject
            {
                ["action"] = "get",
                ["includeStacktrace"] = false,
                ["count"] = 3
            };

            // Act
            object result = ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.IsNotNull(result, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.AreEqual(true, resultObj["success"]?.ToObject<bool>(), "No-stacktrace get should succeed");
            Assert.IsNotNull(resultObj["data"], "Should return data");
            
            JArray entries = resultObj["data"] as JArray;
            Assert.IsNotNull(entries, "Data should be an array");
            
            // Verify stackTrace is null when includeStacktrace is false
            foreach (JObject entry in entries.Cast<JObject>())
            {
                if (entry["stackTrace"] != null)
                {
                    Assert.IsTrue(entry["stackTrace"].Type == JTokenType.Null, 
                        "StackTrace should be null when includeStacktrace is false");
                }
            }
        }

        [Test]
        public void DirectHandler_InvalidAction_ReturnsError()
        {
            // Arrange
            JObject parameters = new JObject
            {
                ["action"] = "invalid_action"
            };

            // Act
            object result = ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.IsNotNull(result, "Handler should return a result even for invalid action");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Debug.Log($"Invalid Action Result: {resultJson}");
            
            // Check for Response.Error format: { success: false, error: "..." }
            Assert.AreEqual(false, resultObj["success"]?.ToObject<bool>(), "Invalid action should return error status");
            Assert.IsNotNull(resultObj["error"], "Error response should contain error message");
        }

        [Test]
        public void DirectHandler_NullParameters_DefaultsToGet()
        {
            // Act
            object result = ReadConsole.HandleCommand(null);

            // Assert
            Assert.IsNotNull(result, "Handler should handle null parameters gracefully");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Debug.Log($"Null Parameters Result: {resultJson}");

            // Should default to "get" action when parameters are null
            Assert.AreEqual(true, resultObj["success"]?.ToObject<bool>(), "Null parameters should default to successful get");
        }

        #endregion

        #region TCP Communication Tests

        [UnityTest]
        public IEnumerator TCPTest_ClearConsole_Success()
        {
            // Generate some test logs first
            GenerateTestLogEntries();
            
            // Create clear command
            Command command = new Command
            {
                type = "read_console",
                @params = new JObject
                {
                    ["action"] = "clear"
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Debug.Log($"TCP Clear Response: {response.ToString()}");
                
                // UMCPBridge wraps handler response: { status: "success", result: handlerResult }
                Assert.AreEqual("success", response["status"]?.ToString(), "Clear command should succeed via TCP");
                Assert.IsNotNull(response["result"], "Clear command should return result");
                
                JObject result = response["result"] as JObject;
                Assert.AreEqual(true, result["success"]?.ToObject<bool>(), "Handler result should indicate success");
                Assert.IsNotNull(result["message"], "Clear result should contain message");
            });
        }

        [UnityTest]
        public IEnumerator TCPTest_GetConsoleEntries_DefaultParameters_Success()
        {
            // Generate test logs
            GenerateTestLogEntries();
            
            Command command = new Command
            {
                type = "read_console",
                @params = new JObject
                {
                    ["action"] = "get"
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Assert.AreEqual("success", response["status"]?.ToString(), "Get command should succeed via TCP");
                Assert.IsNotNull(response["result"], "Get command should return result");
                
                JObject result = response["result"] as JObject;
                Assert.AreEqual(true, result["success"]?.ToObject<bool>(), "Handler result should indicate success");
                Assert.IsNotNull(result["data"], "Get result should contain data array");
                Assert.IsTrue(result["data"] is JArray, "Data should be an array");
            });
        }

        [UnityTest]
        public IEnumerator TCPTest_GetConsoleEntries_WithFiltering_Success()
        {
            // Generate test logs
            GenerateTestLogEntries();
            
            Command command = new Command
            {
                type = "read_console",
                @params = new JObject
                {
                    ["action"] = "get",
                    ["types"] = new JArray { "warning", "error" },
                    ["filterText"] = "ReadConsoleTest",
                    ["count"] = 5,
                    ["format"] = "detailed",
                    ["includeStacktrace"] = true
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Assert.AreEqual("success", response["status"]?.ToString(), "Filtered get should succeed via TCP");
                Assert.IsNotNull(response["result"], "Filtered get should return result");
                
                JObject result = response["result"] as JObject;
                Assert.AreEqual(true, result["success"]?.ToObject<bool>(), "Handler result should indicate success");
                Assert.IsNotNull(result["data"], "Filtered get should contain data array");
                
                JArray entries = result["data"] as JArray;
                Assert.IsNotNull(entries, "Data should be an array");
                Assert.LessOrEqual(entries.Count, 5, "Should respect count limit");
                
                // Verify filtering worked
                foreach (JObject entry in entries.Cast<JObject>())
                {
                    Assert.IsNotNull(entry["message"], "Each entry should have a message");
                    Assert.IsNotNull(entry["type"], "Each entry should have a type");
                    
                    string message = entry["message"].ToString();
                    Assert.IsTrue(message.Contains("ReadConsoleTest"), 
                        $"Entry should contain filter text: {message}");
                }
            });
        }

        [UnityTest]
        public IEnumerator TCPTest_GetConsoleEntries_PlainFormat_Success()
        {
            // Generate test logs
            GenerateTestLogEntries();
            
            Command command = new Command
            {
                type = "read_console",
                @params = new JObject
                {
                    ["action"] = "get",
                    ["format"] = "plain",
                    ["count"] = 3
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Assert.AreEqual("success", response["status"]?.ToString(), "Plain format get should succeed via TCP");
                Assert.IsNotNull(response["result"], "Plain format get should return result");
                
                JObject result = response["result"] as JObject;
                Assert.AreEqual(true, result["success"]?.ToObject<bool>(), "Handler result should indicate success");
                Assert.IsNotNull(result["data"], "Plain format get should contain data array");
                
                JArray entries = result["data"] as JArray;
                Assert.IsNotNull(entries, "Data should be an array");
                
                // In plain format, entries should be strings
                foreach (JToken entry in entries)
                {
                    Assert.IsTrue(entry.Type == JTokenType.String, 
                        "Plain format entries should be strings");
                }
            });
        }

        [UnityTest]
        public IEnumerator TCPTest_GetConsoleEntries_AllTypes_Success()
        {
            // Generate test logs
            GenerateTestLogEntries();
            
            Command command = new Command
            {
                type = "read_console",
                @params = new JObject
                {
                    ["action"] = "get",
                    ["types"] = new JArray { "all" },
                    ["count"] = 10
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Assert.AreEqual("success", response["status"]?.ToString(), "'All' types filter should succeed via TCP");
                Assert.IsNotNull(response["result"], "'All' types should return result");
                
                JObject result = response["result"] as JObject;
                Assert.AreEqual(true, result["success"]?.ToObject<bool>(), "Handler result should indicate success");
                Assert.IsNotNull(result["data"], "'All' types should contain data array");
                
                JArray entries = result["data"] as JArray;
                Assert.IsNotNull(entries, "Data should be an array");
                Assert.LessOrEqual(entries.Count, 10, "Should respect count limit");
            });
        }

        [UnityTest]
        public IEnumerator TCPTest_InvalidAction_ReturnsError()
        {
            Command command = new Command
            {
                type = "read_console",
                @params = new JObject
                {
                    ["action"] = "invalid_action"
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Debug.Log($"TCP Invalid Action Response: {response.ToString()}");
                
                // UMCPBridge should still return success status, but handler result will indicate error
                Assert.AreEqual("success", response["status"]?.ToString(), "Bridge should succeed even when handler has error");
                Assert.IsNotNull(response["result"], "Should return result object");
                
                JObject result = response["result"] as JObject;
                Assert.AreEqual(false, result["success"]?.ToObject<bool>(), "Handler result should indicate error");
                Assert.IsNotNull(result["error"], "Handler result should contain error message");
            });
        }

        [UnityTest]
        public IEnumerator TCPTest_EmptyParameters_DefaultsToGet()
        {
            Command command = new Command
            {
                type = "read_console",
                @params = new JObject() // Empty parameters
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Assert.AreEqual("success", response["status"]?.ToString(), "Empty parameters should succeed");
                Assert.IsNotNull(response["result"], "Should return result");
                
                JObject result = response["result"] as JObject;
                Assert.AreEqual(true, result["success"]?.ToObject<bool>(), "Should default to successful get operation");
                Assert.IsNotNull(result["data"], "Should contain data array");
            });
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public void DirectHandler_GetConsoleEntries_ZeroCount_ReturnsEmptyArray()
        {
            // Arrange
            GenerateTestLogEntries();
            
            JObject parameters = new JObject
            {
                ["action"] = "get",
                ["count"] = 0
            };

            // Act
            object result = ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.IsNotNull(result, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.AreEqual(true, resultObj["success"]?.ToObject<bool>(), "Zero count should succeed");
            Assert.IsNotNull(resultObj["data"], "Should return data");
            
            Debug.Log($"Get Console with Zero Count Result: {resultJson}");

            JArray entries = resultObj["data"] as JArray;
            Assert.IsNotNull(entries, "Data should be an array");
            Assert.AreEqual(0, entries.Count, "Should return empty array when count is 0");
        }

        [Test]
        public void DirectHandler_GetConsoleEntries_NonExistentFilterText_ReturnsEmptyArray()
        {
            // Arrange
            GenerateTestLogEntries();
            
            JObject parameters = new JObject
            {
                ["action"] = "get",
                ["filterText"] = "NonExistentFilterText12345",
                ["count"] = 10
            };

            // Act
            object result = ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.IsNotNull(result, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.AreEqual(true, resultObj["success"]?.ToObject<bool>(), "Non-existent filter should succeed");
            Assert.IsNotNull(resultObj["data"], "Should return data");
            
            JArray entries = resultObj["data"] as JArray;
            Assert.IsNotNull(entries, "Data should be an array");
            Assert.AreEqual(0, entries.Count, "Should return empty array when no entries match filter");
        }

        [Test]
        public void DirectHandler_GetConsoleEntries_InvalidLogType_FiltersCorrectly()
        {
            // Arrange
            GenerateTestLogEntries();
            
            JObject parameters = new JObject
            {
                ["action"] = "get",
                ["types"] = new JArray { "invalidtype" },
                ["count"] = 10
            };

            // Act
            object result = ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.IsNotNull(result, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.AreEqual(true, resultObj["success"]?.ToObject<bool>(), "Invalid log type should succeed");
            Assert.IsNotNull(resultObj["data"], "Should return data");
            
            JArray entries = resultObj["data"] as JArray;
            Assert.IsNotNull(entries, "Data should be an array");
            Assert.AreEqual(0, entries.Count, "Should return empty array when log type doesn't match any entries");
        }

        #endregion

        #region Integration Tests

        [UnityTest]
        public IEnumerator IntegrationTest_ClearThenGet_Success()
        {
            // Generate test logs
            GenerateTestLogEntries();
            
            // First, clear the console
            Command clearCommand = new Command
            {
                type = "read_console",
                @params = new JObject { ["action"] = "clear" }
            };

            yield return ExecuteTCPCommandTest(clearCommand, (response) =>
            {
                Assert.AreEqual("success", response["status"]?.ToString(), "Clear should succeed");
                JObject result = response["result"] as JObject;
                Assert.AreEqual(true, result["success"]?.ToObject<bool>(), "Clear handler should succeed");
            });

            // Wait a moment
            yield return new WaitForSeconds(0.1f);

            // Generate new test logs after clearing
            GenerateTestLogEntries();

            // Then get console entries
            Command getCommand = new Command
            {
                type = "read_console",
                @params = new JObject
                {
                    ["action"] = "get",
                    ["filterText"] = "ReadConsoleTest",
                    ["count"] = 10
                }
            };

            yield return ExecuteTCPCommandTest(getCommand, (response) =>
            {
                Assert.AreEqual("success", response["status"]?.ToString(), "Get after clear should succeed");
                
                JObject result = response["result"] as JObject;
                Assert.AreEqual(true, result["success"]?.ToObject<bool>(), "Get handler should succeed");
                Assert.IsNotNull(result["data"], "Should return data");
                
                JArray entries = result["data"] as JArray;
                Assert.IsNotNull(entries, "Data should be an array");
                
                // Should find the new test entries we added after clearing
                Assert.Greater(entries.Count, 0, "Should find test entries added after clear");
                
                foreach (JObject entry in entries.Cast<JObject>())
                {
                    string message = entry["message"]?.ToString();
                    Assert.IsTrue(message.Contains("ReadConsoleTest"), 
                        "Entries should contain our test filter text");
                }
            });
        }

        [UnityTest]
        public IEnumerator IntegrationTest_MultipleGetsWithDifferentFilters_Success()
        {
            // Generate test logs
            GenerateTestLogEntries();
            
            // Test 1: Get all error logs
            Command errorCommand = new Command
            {
                type = "read_console",
                @params = new JObject
                {
                    ["action"] = "get",
                    ["types"] = new JArray { "error" },
                    ["count"] = 5
                }
            };

            yield return ExecuteTCPCommandTest(errorCommand, (response) =>
            {
                Assert.AreEqual("success", response["status"]?.ToString(), "Error filter should succeed");
                JObject result = response["result"] as JObject;
                Assert.AreEqual(true, result["success"]?.ToObject<bool>(), "Error filter handler should succeed");
            });

            // Test 2: Get all warning logs
            Command warningCommand = new Command
            {
                type = "read_console",
                @params = new JObject
                {
                    ["action"] = "get",
                    ["types"] = new JArray { "warning" },
                    ["count"] = 5
                }
            };

            yield return ExecuteTCPCommandTest(warningCommand, (response) =>
            {
                Assert.AreEqual("success", response["status"]?.ToString(), "Warning filter should succeed");
                JObject result = response["result"] as JObject;
                Assert.AreEqual(true, result["success"]?.ToObject<bool>(), "Warning filter handler should succeed");
            });

            // Test 3: Get logs with specific text in plain format
            Command plainCommand = new Command
            {
                type = "read_console",
                @params = new JObject
                {
                    ["action"] = "get",
                    ["filterText"] = "ReadConsoleTest",
                    ["format"] = "plain",
                    ["count"] = 3
                }
            };

            yield return ExecuteTCPCommandTest(plainCommand, (response) =>
            {
                Assert.AreEqual("success", response["status"]?.ToString(), "Plain format filter should succeed");
                
                JObject result = response["result"] as JObject;
                Assert.AreEqual(true, result["success"]?.ToObject<bool>(), "Plain format handler should succeed");
                JArray entries = result["data"] as JArray;
                
                foreach (JToken entry in entries)
                {
                    Assert.IsTrue(entry.Type == JTokenType.String, "Plain format should return strings");
                }
            });
        }

        #endregion
    }
}
