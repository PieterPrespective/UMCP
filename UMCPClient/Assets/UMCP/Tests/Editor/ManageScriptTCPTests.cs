using NUnit.Framework;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UMCP.Editor;
using UMCP.Editor.Tools.ManageScripts;
using static UMCP.Editor.Tools.ManageScripts.SymbolSearchUtility;
using Newtonsoft.Json;

namespace UMCP.Tests.Editor
{
    /// <summary>
    /// TCP interface tests for ManageScripts tool, testing FindSymbolDefinition and DecompileClass functionality through UMCPBridge.
    /// </summary>
    public class ManageScriptTCPTests
    {
        private const int unityPort = 16999;
        
        /// <summary>
        /// Command structure for TCP communication
        /// </summary>
        private class Command
        {
            public string type;
            public JObject @params;
        }

        #region TCP Communication Helpers

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
                    Debug.Log($"Sending command: {commandJson}");

                    await stream.WriteAsync(commandBytes, 0, commandBytes.Length);

                    byte[] buffer = new byte[65536]; // Large buffer for potentially large responses
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    
                    string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    return JObject.Parse(responseJson);
                }
            }
        }

        /// <summary>
        /// Helper method to execute TCP command test in a coroutine
        /// </summary>
        private async Task ExecuteTCPCommandTest(Command command, System.Action<JObject> responseValidator)
        {
            Debug.Log($"Executing TCP command test for action: {command.@params["action"]}");
            JObject response = await SendCommandViaTCP(command);
            responseValidator(response);
        }

        /// <summary>
        /// Helper method to wait for task completion with timeout
        /// </summary>
        private IEnumerator WaitForTCPCommandTest(Command command, System.Action<JObject> responseValidator, string operationName = "unknown",  int timeoutSeconds = 10)
        {
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var task = ExecuteTCPCommandTest(command, responseValidator);

            var completedTask = Task.WhenAny(task, timeoutTask).Result;

            if (completedTask == timeoutTask)
            {
                Assert.Fail($"{operationName} operation timed out after {timeoutSeconds} seconds");
            }

            if (task.IsFaulted)
            {
                Assert.Fail($"{operationName} operation failed: {task.Exception?.GetBaseException().Message}");
            }

            yield return null; // Wait one frame
        }







        #endregion

        #region FindSymbolDefinition TCP Tests

        [UnityTest]
        [Ignore("TCP testing doesn't work while an active connection is present (so we tend to test this via integration tests from the server)")]
        public IEnumerator TCPTest_FindSymbolDefinition_ValidClass_ReturnsResult()
        {
            // Test finding a known Unity class
            Command command = new Command
            {
                type = "manage_scripts",
                @params = new JObject
                {
                    ["action"] = "find_symbol_definition",
                    ["Symbol"] = "GameObject",
                    ["NoOfResults"] = 1
                }
            };

            yield return WaitForTCPCommandTest(command, (response) =>
            {
                Debug.Log($"FindSymbolDefinition Response: {response.ToString()}");
                
                // UMCPBridge wraps handler response: { status: "success", result: handlerResult }
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Bridge should return success");
                Assert.That(response["result"], Is.Not.Null, "Should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Handler should indicate success");
                Assert.That(result["data"], Is.Not.Null, "Should contain data");
                
                // Check if results contain expected structure
                JArray resultsList = result["data"] as JArray;
                if (resultsList != null && resultsList.Count > 0)
                {
                    JObject firstResult = resultsList[0] as JObject;
                    // Should have either LibraryPath or FilePath
                    bool hasPath = firstResult["LibraryPath"] != null || firstResult["FilePath"] != null;
                    Assert.That(hasPath, Is.True, "Result should contain either LibraryPath or FilePath");
                }
            }, "TCPTest_FindSymbolDefinition_ValidClass_ReturnsResult", 30);
        }

        [UnityTest]
        public IEnumerator TCPTest_FindSymbolDefinition_InvalidSymbol_ReturnsEmptyResult()
        {
            // Test finding a non-existent symbol
            string nonExistentSymbol = "NonExistentClass_" + System.Guid.NewGuid().ToString("N")[0..8];
            
            Command command = new Command
            {
                type = "manage_scripts",
                @params = new JObject
                {
                    ["action"] = "find_symbol_definition",
                    ["Symbol"] = nonExistentSymbol,
                    ["NoOfResults"] = 1
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Debug.Log($"FindSymbolDefinition NonExistent Response: {response.ToString()}");
                
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Bridge should return success");
                Assert.That(response["result"], Is.Not.Null, "Should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Handler should indicate success");
                Assert.That(result["message"]?.ToString(), Does.Contain("Found 0 results"), 
                    "Should indicate no results found");
            });
        }

        [UnityTest]
        public IEnumerator TCPTest_FindSymbolDefinition_MissingSymbol_ReturnsError()
        {
            // Test with missing Symbol parameter
            Command command = new Command
            {
                type = "manage_scripts",
                @params = new JObject
                {
                    ["action"] = "find_symbol_definition"
                    // Symbol parameter missing
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Debug.Log($"FindSymbolDefinition Missing Symbol Response: {response.ToString()}");
                
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Bridge should return success");
                Assert.That(response["result"], Is.Not.Null, "Should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(false), "Handler should indicate error");
                Assert.That(result["error"]?.ToString(), Does.Contain("Symbol parameter is required"), 
                    "Should indicate missing symbol parameter");
            });
        }

        [UnityTest]
        public IEnumerator TCPTest_FindSymbolDefinition_WithMultipleResults_Success()
        {
            // Test finding a symbol with multiple results requested
            Command command = new Command
            {
                type = "manage_scripts",
                @params = new JObject
                {
                    ["action"] = "find_symbol_definition",
                    ["Symbol"] = "Debug",
                    ["NoOfResults"] = 3
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Debug.Log($"FindSymbolDefinition Multiple Results Response: {response.ToString()}");
                
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Bridge should return success");
                Assert.That(response["result"], Is.Not.Null, "Should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Handler should indicate success");
                Assert.That(result["data"], Is.Not.Null, "Should contain data");
                
                JArray resultsList = result["data"] as JArray;
                if (resultsList != null)
                {
                    Assert.LessOrEqual(resultsList.Count, 3, "Should respect NoOfResults limit");
                }
            });
        }

        #endregion

        #region DecompileClass TCP Tests

        [UnityTest]
        public IEnumerator TCPTest_DecompileClass_ValidClass_CreatesFile()
        {
            // Prepare test output file
            string testOutputFile = Path.Combine(Application.temporaryCachePath, 
                $"TestDecompile_{System.Guid.NewGuid().ToString("N")[0..8]}.cs");
            
            // Clean up any existing file
            if (File.Exists(testOutputFile))
                File.Delete(testOutputFile);

            Command command = new Command
            {
                type = "manage_scripts",
                @params = new JObject
                {
                    ["action"] = "decompile_class",
                    ["LibraryPath"] = "UnityEngine.CoreModule",
                    ["LibraryClass"] = "UnityEngine.GameObject",
                    ["OutputFileName"] = testOutputFile,
                    ["OverrideExistingFile"] = true
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Debug.Log($"DecompileClass Response: {response.ToString()}");
                
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Bridge should return success");
                Assert.That(response["result"], Is.Not.Null, "Should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Handler should indicate success");
                Assert.That(result["data"], Is.Not.Null, "Should contain data");
                
                JObject data = result["data"] as JObject;
                Assert.That(data["FilePath"]?.ToString(), Is.EqualTo(testOutputFile), 
                    "Should return correct output file path");
                
                // Verify file was created
                Assert.That(File.Exists(testOutputFile), Is.True, "Decompiled file should exist");
                
                // Clean up
                if (File.Exists(testOutputFile))
                    File.Delete(testOutputFile);
            });
        }

        [UnityTest]
        public IEnumerator TCPTest_DecompileClass_MissingLibraryPath_ReturnsError()
        {
            Command command = new Command
            {
                type = "manage_scripts",
                @params = new JObject
                {
                    ["action"] = "decompile_class",
                    // LibraryPath missing
                    ["LibraryClass"] = "UnityEngine.GameObject"
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Debug.Log($"DecompileClass Missing LibraryPath Response: {response.ToString()}");
                
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Bridge should return success");
                Assert.That(response["result"], Is.Not.Null, "Should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(false), "Handler should indicate error");
                Assert.That(result["error"], Is.Not.Null, "Should contain error message");
            });
        }

        [UnityTest]
        public IEnumerator TCPTest_DecompileClass_InvalidClass_ReturnsError()
        {
            string testOutputFile = Path.Combine(Application.temporaryCachePath, 
                $"TestDecompile_{System.Guid.NewGuid().ToString("N")[0..8]}.cs");

            Command command = new Command
            {
                type = "manage_scripts",
                @params = new JObject
                {
                    ["action"] = "decompile_class",
                    ["LibraryPath"] = "UnityEngine.CoreModule",
                    ["LibraryClass"] = "UnityEngine.NonExistentClass",
                    ["OutputFileName"] = testOutputFile
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Debug.Log($"DecompileClass Invalid Class Response: {response.ToString()}");
                
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Bridge should return success");
                Assert.That(response["result"], Is.Not.Null, "Should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(false), "Handler should indicate error");
                Assert.That(result["error"], Is.Not.Null, "Should contain error message");
                
                // File should not be created
                Assert.That(File.Exists(testOutputFile), Is.False, "File should not be created for invalid class");
            });
        }

        #endregion

        #region Invalid Action Tests

        [UnityTest]
        public IEnumerator TCPTest_ManageScripts_InvalidAction_ReturnsError()
        {
            Command command = new Command
            {
                type = "manage_scripts",
                @params = new JObject
                {
                    ["action"] = "invalid_action",
                    ["Symbol"] = "Test"
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Debug.Log($"ManageScripts Invalid Action Response: {response.ToString()}");
                
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Bridge should return success");
                Assert.That(response["result"], Is.Not.Null, "Should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(false), "Handler should indicate error");
                Assert.That(result["error"]?.ToString(), Does.Contain("Unknown action"), 
                    "Should indicate unknown action");
            });
        }

        [UnityTest]
        public IEnumerator TCPTest_ManageScripts_MissingAction_ReturnsError()
        {
            Command command = new Command
            {
                type = "manage_scripts",
                @params = new JObject
                {
                    // action parameter missing
                    ["Symbol"] = "Test"
                }
            };

            yield return ExecuteTCPCommandTest(command, (response) =>
            {
                Debug.Log($"ManageScripts Missing Action Response: {response.ToString()}");
                
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Bridge should return success");
                Assert.That(response["result"], Is.Not.Null, "Should return result");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(false), "Handler should indicate error");
                Assert.That(result["error"]?.ToString(), Does.Contain("Action parameter is required"), 
                    "Should indicate missing action");
            });
        }

        #endregion

        #region Integration Tests

        [UnityTest]
        public IEnumerator TCPTest_Integration_FindAndDecompile_Success()
        {
            // Step 1: Find a symbol definition
            Command findCommand = new Command
            {
                type = "manage_scripts",
                @params = new JObject
                {
                    ["action"] = "find_symbol_definition",
                    ["Symbol"] = "UnityEditor.EditorWindow",
                    ["NoOfResults"] = 1
                }
            };

            string libraryPath = null;
            string libraryClass = null;

            yield return ExecuteTCPCommandTest(findCommand, (response) =>
            {
                Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Find should succeed");
                
                JObject result = response["result"] as JObject;
                Assert.That(result["success"]?.ToObject<bool>(), Is.EqualTo(true), "Find handler should succeed");
                
                JArray results = result["data"] as JArray;
                if (results != null && results.Count > 0)
                {
                    JObject firstResult = results[0] as JObject;
                    libraryPath = firstResult["LibraryPath"]?.ToString();
                    libraryClass = firstResult["LibraryClass"]?.ToString();
                }
            });

            // Step 2: If we found a library class, try to decompile it
            if (!string.IsNullOrEmpty(libraryPath) && !string.IsNullOrEmpty(libraryClass))
            {
                string testOutputFile = Path.Combine(Application.temporaryCachePath, 
                    $"TestIntegration_{System.Guid.NewGuid().ToString("N")[0..8]}.cs");

                Command decompileCommand = new Command
                {
                    type = "manage_scripts",
                    @params = new JObject
                    {
                        ["action"] = "decompile_class",
                        ["LibraryPath"] = libraryPath,
                        ["LibraryClass"] = libraryClass,
                        ["OutputFileName"] = testOutputFile,
                        ["OverrideExistingFile"] = true
                    }
                };

                yield return ExecuteTCPCommandTest(decompileCommand, (response) =>
                {
                    Assert.That(response["status"]?.ToString(), Is.EqualTo("success"), "Decompile should succeed");
                    
                    JObject result = response["result"] as JObject;
                    // Decompile might fail for some classes, but the TCP interface should work
                    Assert.That(result, Is.Not.Null, "Should return a result");
                    
                    // Clean up if file was created
                    if (File.Exists(testOutputFile))
                        File.Delete(testOutputFile);
                });
            }
        }

        #endregion
    }
}