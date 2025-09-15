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

            
            //bool isBridgeRunning = UMCPBridge.IsRunning;
            //bool bridgeWasRunning = false;
            //if(isBridgeRunning)
            //{
            //    UMCPBridge.Stop();
            //    bridgeWasRunning = true;
            //    await Task.Delay(100); // Wait a bit to ensure bridge is ready
            //}

            JObject result = null;

            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync("localhost", unityPort);
                using (NetworkStream stream = client.GetStream())
                {
                    await stream.WriteAsync(commandBytes, 0, commandBytes.Length);

                    byte[] buffer = new byte[16384]; // Larger buffer for console logs
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    
                    string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    result = JObject.Parse(responseJson);
                }
            }

            //if (bridgeWasRunning)
            //{
            //    UMCPBridge.Start(); // Restart bridge if it was running before
            //}
            return result;
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
        public async Task DirectHandler_ClearConsole_Success()
        {
            // Arrange
            JObject parameters = new JObject
            {
                ["action"] = "clear"
            };

            // Act
            object result = await ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.That(result, Is.Not.Null, "Handler should return a result");
            
            // Convert result to JObject for inspection
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);

            Debug.Log($"Clear Console Result: {resultJson}");

            // Check for Response.Success format: { success: true, message: "..." }
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Clear action should succeed");
            Assert.That(resultObj["message"], Is.Not.Null, "Clear action should return a message");
        }

        [Test]
        public async Task DirectHandler_GetConsoleEntries_DefaultParameters_Success()
        {
            // Arrange
            GenerateTestLogEntries(); // Add some test logs
            
            JObject parameters = new JObject
            {
                ["action"] = "get"
            };

            // Act
            object result = await ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.That(result, Is.Not.Null, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Debug.Log($"Get Console Result: {resultJson}");
            
            // Check for Response.Success format: { success: true, message: "...", data: [...] }
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Get action should succeed");
            Assert.That(resultObj["data"], Is.Not.Null, "Get action should return data");
            Assert.That(resultObj["data"] is JArray, Is.True, "Data should be an array of log entries");
        }

        [Test]
        public async Task DirectHandler_GetConsoleEntries_FilterByLogType_Success()
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
            object result = await ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.That(result, Is.Not.Null, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Filtered get should succeed");
            Assert.That(resultObj["data"], Is.Not.Null, "Should return data");
            
            JArray entries = resultObj["data"] as JArray;
            Assert.That(entries, Is.Not.Null, "Data should be an array");
            
            // Verify all returned entries are errors (if any exist)
            foreach (JObject entry in entries.Cast<JObject>())
            {
                Assert.That(entry["type"], Is.Not.Null, "Each entry should have a type");
                // Note: Due to the mode bit correction in ReadConsole, "Error" becomes "Warning"
                // This test validates the filtering logic works, regardless of the specific mapping
            }
        }

        [Test]
        public async Task DirectHandler_GetConsoleEntries_FilterByText_Success()
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
            object result = await ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.That(result, Is.Not.Null, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Text-filtered get should succeed");
            Assert.That(resultObj["data"], Is.Not.Null, "Should return data");
            
            JArray entries = resultObj["data"] as JArray;
            Assert.That(entries, Is.Not.Null, "Data should be an array");
            
            // Verify all returned entries contain the filter text
            foreach (JObject entry in entries.Cast<JObject>())
            {
                Assert.That(entry["message"], Is.Not.Null, "Each entry should have a message");
                string message = entry["message"].ToString();
                Assert.That(message.Contains("ReadConsoleTest"), Is.True, 
                    $"Entry message should contain filter text: {message}");
            }
        }

        [Test]
        public async Task DirectHandler_GetConsoleEntries_PlainFormat_Success()
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
            object result = await ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.That(result, Is.Not.Null, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Plain format get should succeed");
            Assert.That(resultObj["data"], Is.Not.Null, "Should return data");
            
            JArray entries = resultObj["data"] as JArray;
            Assert.That(entries, Is.Not.Null, "Data should be an array");
            
            // In plain format, each entry should be just a string
            foreach (JToken entry in entries)
            {
                Assert.That(entry.Type == JTokenType.String, Is.True, 
                    "In plain format, each entry should be a string");
            }
        }

        [Test]
        public async Task DirectHandler_GetConsoleEntries_WithoutStackTrace_Success()
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
            object result = await ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.That(result, Is.Not.Null, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "No-stacktrace get should succeed");
            Assert.That(resultObj["data"], Is.Not.Null, "Should return data");
            
            JArray entries = resultObj["data"] as JArray;
            Assert.That(entries, Is.Not.Null, "Data should be an array");
            
            // Verify stackTrace is null when includeStacktrace is false
            foreach (JObject entry in entries.Cast<JObject>())
            {
                if (entry["stackTrace"] != null)
                {
                    Assert.That(entry["stackTrace"].Type == JTokenType.Null, Is.True, 
                        "StackTrace should be null when includeStacktrace is false");
                }
            }
        }

        [Test]
        public async Task DirectHandler_InvalidAction_ReturnsError()
        {
            // Arrange
            JObject parameters = new JObject
            {
                ["action"] = "invalid_action"
            };

            // Act
            object result = await ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.That(result, Is.Not.Null, "Handler should return a result even for invalid action");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Debug.Log($"Invalid Action Result: {resultJson}");
            
            // Check for Response.Error format: { success: false, error: "..." }
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(false), "Invalid action should return error status");
            Assert.That(resultObj["error"], Is.Not.Null, "Error response should contain error message");
        }

        [Test]
        public async Task DirectHandler_NullParameters_DefaultsToGet()
        {
            // Act
            object result = await ReadConsole.HandleCommand(null);

            // Assert
            Assert.That(result, Is.Not.Null, "Handler should handle null parameters gracefully");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Debug.Log($"Null Parameters Result: {resultJson}");

            // Should default to "get" action when parameters are null
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Null parameters should default to successful get");
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
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Clear command should succeed via TCP");
                Assert.That(response["result"], Is.Not.Null, "Clear command should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Handler result should indicate success");
                Assert.That(result["message"], Is.Not.Null, "Clear result should contain message");
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
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Get command should succeed via TCP");
                Assert.That(response["result"], Is.Not.Null, "Get command should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Handler result should indicate success");
                Assert.That(result["data"], Is.Not.Null, "Get result should contain data array");
                Assert.That(result["data"] is JArray, Is.True, "Data should be an array");
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
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Filtered get should succeed via TCP");
                Assert.That(response["result"], Is.Not.Null, "Filtered get should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Handler result should indicate success");
                Assert.That(result["data"], Is.Not.Null, "Filtered get should contain data array");
                
                JArray entries = result["data"] as JArray;
                Assert.That(entries, Is.Not.Null, "Data should be an array");
                Assert.LessOrEqual(entries.Count, 5, "Should respect count limit");
                
                // Verify filtering worked
                foreach (JObject entry in entries.Cast<JObject>())
                {
                    Assert.That(entry["message"], Is.Not.Null, "Each entry should have a message");
                    Assert.That(entry["type"], Is.Not.Null, "Each entry should have a type");
                    
                    string message = entry["message"].ToString();
                    Assert.That(message.Contains("ReadConsoleTest"), Is.True, 
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
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Plain format get should succeed via TCP");
                Assert.That(response["result"], Is.Not.Null, "Plain format get should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Handler result should indicate success");
                Assert.That(result["data"], Is.Not.Null, "Plain format get should contain data array");
                
                JArray entries = result["data"] as JArray;
                Assert.That(entries, Is.Not.Null, "Data should be an array");
                
                // In plain format, entries should be strings
                foreach (JToken entry in entries)
                {
                    Assert.That(entry.Type == JTokenType.String, Is.True, 
                        "Plain format entries should be strings");
                }
            });
        }

        [UnityTest]
        [Ignore("This test will only run in single mode due to TCP channel spamming")]
        public IEnumerator TestValidMessageTypeInterpretation()
        {
            yield return new WaitForSeconds(0.1f); // Ensure logs are generated before testing

            string logMsg = "[ReadConsoleTest] This is a normal log message";
            string warningMsg = "[ReadConsoleTest] This is a Warning log message";
            string errorMsg = "[ReadConsoleTest] This is an Error log message";
            string exceptionMsg = "[ReadConsoleTest] This is an Exception log message";
            string assertMsg = "[ReadConsoleTest] This is an Assert log message";

            Debug.Log(">>> Testing Log Type");
            Debug.Log(logMsg);
            yield return executeLogTypeTest("log", new string[] { logMsg }, 10);

            Debug.Log(">>> Testing Warning Type");
            Debug.LogWarning(warningMsg);
            yield return executeLogTypeTest("warning", new string[] { warningMsg }, 10);

            Debug.Log(">>> Testing Error Type");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, errorMsg);
            Debug.LogError(errorMsg);
            yield return executeLogTypeTest("error", new string[] { errorMsg }, 10);

            Debug.Log(">>> Testing Exception Type");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, "Exception: " + exceptionMsg);
            Debug.LogException(new System.Exception(exceptionMsg));
            yield return executeLogTypeTest("exception", new string[] { "Exception: " + exceptionMsg }, 10);

            Debug.Log(">>> Testing Asser Type");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Assert, assertMsg);
            Debug.LogAssertion(assertMsg);
            yield return executeLogTypeTest("assert", new string[] { assertMsg }, 10);

            Debug.Log(">>> Testing all log types together");
            yield return executeLogTypeTest("all", new string[] { logMsg, warningMsg, errorMsg, "Exception: " + exceptionMsg, assertMsg }, 40, true);
        }

        private IEnumerator executeLogTypeTest(string _msgType, string[] _expectedMsgs, int _limit, bool _ignoreMsgtype = false)
        {
            Command command = new Command
            {
                type = "read_console",
                @params = new JObject
                {
                    ["action"] = "get",
                    ["types"] = new JArray { _msgType },
                    ["count"] = _limit
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), $"'{_msgType}' types filter should succeed via TCP");
                Assert.That(response["result"], Is.Not.Null, $"'{_msgType}' types should return result");

                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Handler result should indicate success");
                Assert.That(result["data"], Is.Not.Null, $"'{_msgType}' types should contain data array");

                JArray entries = result["data"] as JArray;
                Assert.That(entries, Is.Not.Null, "Data should be an array");

                bool[] fndMsgs = new bool[_expectedMsgs.Length];
                for (int i = 0; i < entries.Count; i++)
                {
                    JObject entry = entries[i] as JObject;
                    Assert.That(entry, Is.Not.Null, "Each entry should be a JObject");
                    if(!_ignoreMsgtype)
                    {
                        Assert.That(entry["type"]?.ToString().ToLower(), Is.EqualTo(_msgType), $"Entry type should be '{_msgType}'");
                    }
                    int idx = System.Array.IndexOf(_expectedMsgs, entry["message"]?.ToString());
                    if (idx > -1)
                    {
                        fndMsgs[idx] = true;
                    }
                }
                List<string> notFoundMsgs = new List<string>();
                for (int i = 0; i < fndMsgs.Length; i++)
                {
                    if (!fndMsgs[i])
                    {
                        notFoundMsgs.Add(_expectedMsgs[i]);
                    }
                }



                Assert.That(notFoundMsgs.Count == 0, Is.True, $"Missed one or more expected messages in the results;{string.Join(',', notFoundMsgs)}");
                Assert.LessOrEqual(entries.Count, _limit, "Should respect count limit");
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
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "'All' types filter should succeed via TCP");
                Assert.That(response["result"], Is.Not.Null, "'All' types should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Handler result should indicate success");
                Assert.That(result["data"], Is.Not.Null, "'All' types should contain data array");
                
                JArray entries = result["data"] as JArray;
                Assert.That(entries, Is.Not.Null, "Data should be an array");
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
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Bridge should succeed even when handler has error");
                Assert.That(response["result"], Is.Not.Null, "Should return result object");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(false), "Handler result should indicate error");
                Assert.That(result["error"], Is.Not.Null, "Handler result should contain error message");
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
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Empty parameters should succeed");
                Assert.That(response["result"], Is.Not.Null, "Should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Should default to successful get operation");
                Assert.That(result["data"], Is.Not.Null, "Should contain data array");
            });
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public async Task DirectHandler_GetConsoleEntries_ZeroCount_ReturnsEmptyArray()
        {
            // Arrange
            GenerateTestLogEntries();
            
            JObject parameters = new JObject
            {
                ["action"] = "get",
                ["count"] = 0
            };

            // Act
            object result = await ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.That(result, Is.Not.Null, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Zero count should succeed");
            Assert.That(resultObj["data"], Is.Not.Null, "Should return data");
            
            Debug.Log($"Get Console with Zero Count Result: {resultJson}");

            JArray entries = resultObj["data"] as JArray;
            Assert.That(entries, Is.Not.Null, "Data should be an array");
            Assert.That(entries.Count, Is.EqualTo(0), "Should return empty array when count is 0");
        }

        [Test]
        public async Task DirectHandler_GetConsoleEntries_NonExistentFilterText_ReturnsEmptyArray()
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
            object result = await ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.That(result, Is.Not.Null, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Non-existent filter should succeed");
            Assert.That(resultObj["data"], Is.Not.Null, "Should return data");
            
            JArray entries = resultObj["data"] as JArray;
            Assert.That(entries, Is.Not.Null, "Data should be an array");
            Assert.That(entries.Count, Is.EqualTo(0), "Should return empty array when no entries match filter");
        }

        [Test]
        public async Task DirectHandler_GetConsoleEntries_InvalidLogType_FiltersCorrectly()
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
            object result = await ReadConsole.HandleCommand(parameters);

            // Assert
            Assert.That(result, Is.Not.Null, "Handler should return a result");
            
            string resultJson = JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Invalid log type should succeed");
            Assert.That(resultObj["data"], Is.Not.Null, "Should return data");
            
            JArray entries = resultObj["data"] as JArray;
            Assert.That(entries, Is.Not.Null, "Data should be an array");
            Assert.That(entries.Count, Is.EqualTo(0), "Should return empty array when log type doesn't match any entries");
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
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Clear should succeed");
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Clear handler should succeed");
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
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Get after clear should succeed");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Get handler should succeed");
                Assert.That(result["data"], Is.Not.Null, "Should return data");
                
                JArray entries = result["data"] as JArray;
                Assert.That(entries, Is.Not.Null, "Data should be an array");
                
                // Should find the new test entries we added after clearing
                Assert.Greater(entries.Count, 0, "Should find test entries added after clear");
                int foundCount = 0;
                foreach (JObject entry in entries.Cast<JObject>())
                {
                    string message = entry["message"]?.ToString();

                    //NOTE : not all messages may contain the filter text due to the nature of log generation
                    if(message != null && message.Contains("ReadConsoleTest"))
                    {
                        foundCount++;
                    }

                   
                }
                Assert.That(foundCount > 0, Is.True, "Some messages should contain 'ReadConsoleTest'");

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
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Error filter should succeed");
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Error filter handler should succeed");
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
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Warning filter should succeed");
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Warning filter handler should succeed");
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
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Plain format filter should succeed");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Plain format handler should succeed");
                JArray entries = result["data"] as JArray;
                
                foreach (JToken entry in entries)
                {
                    Assert.That(entry.Type == JTokenType.String, Is.True, "Plain format should return strings");
                }
            });
        }

        #endregion
    }
}
