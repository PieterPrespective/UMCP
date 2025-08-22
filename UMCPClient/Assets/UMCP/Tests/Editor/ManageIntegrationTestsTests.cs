using NUnit.Framework;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;
using UMCP.Editor.Tools;
using UMCP.Editor.Testing;

namespace UMCP.Tests.Editor
{
    /// <summary>
    /// Unit tests for ManageIntegrationTests functionality.
    /// Tests the integration test management system and harness validation.
    /// </summary>
    public class ManageIntegrationTestsTests
    {
        #region Validation Tests Using Reflection

        [Test]
        [Description("Tests that all integration test harnesses can be discovered via reflection")]
        public void GetAllHarnesses_DiscoversAllHarnessClasses_Successfully()
        {
            // Act
            JObject parameters = new JObject
            {
                ["action"] = "get_all_harnesses"
            };

            object result = ManageIntegrationTests.HandleCommand(parameters);

            // Assert
            Assert.That(result, Is.Not.Null, "Should return a result object");

            string resultJson = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);

            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Getting harnesses should succeed");
            Assert.That(resultObj["data"], Is.Not.Null, "Should return harness data");

            JObject data = resultObj["data"] as JObject;
            Assert.That(data["totalHarnesses"], Is.Not.Null, "Should report total harness count");
            Assert.That(data["results"], Is.Not.Null, "Should return harness results");

            JArray results = data["results"] as JArray;
            Assert.That(results, Is.Not.Null, "Results should be an array");

            // Check each harness found
            foreach (JObject harnessResult in results.Cast<JObject>())
            {
                Assert.That(harnessResult["className"], Is.Not.Null, "Each result should have a className");
                Assert.That(harnessResult["fullName"], Is.Not.Null, "Each result should have a fullName");
                Assert.That(harnessResult["location"], Is.Not.Null, "Each result should have a location");
                Assert.That(harnessResult["isValidLocation"], Is.Not.Null, "Each result should have isValidLocation");
                
                string className = harnessResult["className"].ToString();
                string location = harnessResult["location"].ToString();
                bool isValidLocation = harnessResult["isValidLocation"].ToObject<bool>();
                
                Debug.Log($"Harness found - {className}: Location={location}, Valid={isValidLocation}");
                
                if (harnessResult["error"] != null)
                {
                    Debug.LogWarning($"Harness {className} error: {harnessResult["error"]}!");
                }
            }
        }

        [Test]
        [Description("Tests that reflection can find and instantiate harness classes")]
        public void ReflectionDiscovery_FindsAndInstantiatesHarnesses_Successfully()
        {
            // Act - Find all harness types using reflection
            List<Type> harnessTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsSubclassOf(typeof(UMCPIntegrationTestHarnass)) && !type.IsAbstract)
                .ToList();

            // Assert
            Assert.That(harnessTypes, Is.Not.Null, "Should find harness types");
            Debug.Log($"Found {harnessTypes.Count} integration test harness classes");

            // Test instantiation of each harness
            foreach (Type harnessType in harnessTypes)
            {
                try
                {
                    UMCPIntegrationTestHarnass instance = Activator.CreateInstance(harnessType) as UMCPIntegrationTestHarnass;
                    Assert.That(instance, Is.Not.Null, $"Should be able to instantiate {harnessType.Name}");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Failed to instantiate or validate harness {harnessType.Name}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Command Handling Tests

        [Test]
        [Description("Tests that HandleCommand validates input parameters correctly")]
        public void HandleCommand_ValidatesParameters_Correctly()
        {
            // Test null parameters
            object nullResult = ManageIntegrationTests.HandleCommand(null);
            string nullResultJson = Newtonsoft.Json.JsonConvert.SerializeObject(nullResult);
            JObject nullResultObj = JObject.Parse(nullResultJson);
            Assert.That(nullResultObj["success"]?.ToObject<bool>(), Is.EqualTo(false), "Should return error for null parameters");

            // Test missing action
            JObject emptyParams = new JObject();
            object emptyResult = ManageIntegrationTests.HandleCommand(emptyParams);
            string emptyResultJson = Newtonsoft.Json.JsonConvert.SerializeObject(emptyResult);
            JObject emptyResultObj = JObject.Parse(emptyResultJson);
            Assert.That(emptyResultObj["success"]?.ToObject<bool>(), Is.EqualTo(false), "Should return error for missing action");

            // Test invalid action
            JObject invalidParams = new JObject { ["action"] = "invalid_action" };
            object invalidResult = ManageIntegrationTests.HandleCommand(invalidParams);
            string invalidResultJson = Newtonsoft.Json.JsonConvert.SerializeObject(invalidResult);
            JObject invalidResultObj = JObject.Parse(invalidResultJson);
            Assert.That(invalidResultObj["success"]?.ToObject<bool>(), Is.EqualTo(false), "Should return error for invalid action");
        }

        [Test]
        [Description("Tests that setup command validates harness class parameter")]
        public void SetupCommand_ValidatesHarnessClass_Correctly()
        {
            // Test missing harnassClass parameter
            JObject setupParams = new JObject { ["action"] = "setup" };
            object result = ManageIntegrationTests.HandleCommand(setupParams);
            
            string resultJson = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(false), "Should return error for missing harnassClass");
            Assert.That(resultObj["error"]?.ToString(), Does.Contain("harnassClass parameter is required"), "Should specify missing parameter");
        }

        [Test]
        [Description("Tests that setup command handles non-existent harness classes")]
        public void SetupCommand_HandlesNonExistentClass_Gracefully()
        {
            // Test non-existent class
            JObject setupParams = new JObject 
            { 
                ["action"] = "setup",
                ["harnassClass"] = "NonExistentHarnassClass"
            };
            
            object result = ManageIntegrationTests.HandleCommand(setupParams);
            
            string resultJson = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(false), "Should return error for non-existent class");
            Assert.That(resultObj["error"]?.ToString(), Does.Contain("not found"), "Should specify class not found");
        }

        [Test]
        [Description("Tests that cleanup command works without active harness")]
        public void CleanupCommand_WorksWithoutActiveHarness_Successfully()
        {
            // Test cleanup without active harness
            JObject cleanupParams = new JObject { ["action"] = "cleanup" };
            object result = ManageIntegrationTests.HandleCommand(cleanupParams);
            
            string resultJson = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Should succeed even without active harness");
            Assert.That(resultObj["message"]?.ToString(), Does.Contain("clean state"), "Should indicate clean state");
        }

        [Test]
        [Description("Tests that getIntegrationTestState command returns current state information")]
        public void GetIntegrationTestState_ReturnsStateInformation_Successfully()
        {
            // Test get state command
            JObject stateParams = new JObject { ["action"] = "get_integration_test_state" };
            object result = ManageIntegrationTests.HandleCommand(stateParams);
            
            string resultJson = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.That(resultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Should successfully return state");
            Assert.That(resultObj["data"], Is.Not.Null, "Should return state data");
            
            JObject data = resultObj["data"] as JObject;
            Assert.That(data["currentLoadedHarnass"], Is.Not.Null, "Should include currentLoadedHarnass");
            Assert.That(data["currentTestState"], Is.Not.Null, "Should include currentTestState");
            Assert.That(data["isTestActive"], Is.Not.Null, "Should include isTestActive");
            Assert.That(data["isInTransition"], Is.Not.Null, "Should include isInTransition");
            Assert.That(data["stateChangeTimestamp"], Is.Not.Null, "Should include stateChangeTimestamp");
            Assert.That(data["lastError"], Is.Not.Null, "Should include lastError");
            Assert.That(data["stateDescription"], Is.Not.Null, "Should include stateDescription");
            Assert.That(data["hasActiveHarnassInstance"], Is.Not.Null, "Should include hasActiveHarnassInstance");
            Assert.That(data["editorIsResponsive"], Is.Not.Null, "Should include editorIsResponsive");
            Assert.That(data["canModifyProjectFiles"], Is.Not.Null, "Should include canModifyProjectFiles");
            
            Debug.Log($"Integration test state: {data["stateDescription"]}");
        }

        [Test]
        [Description("Tests polling workflow with setup and state monitoring")]
        public void PollingWorkflow_SetupAndStateMonitoring_WorksCorrectly()
        {
            // First, ensure we start in a clean state
            JObject cleanupParams = new JObject { ["action"] = "cleanup" };
            ManageIntegrationTests.HandleCommand(cleanupParams);

            // Get initial state
            JObject stateParams = new JObject { ["action"] = "get_integration_test_state" };
            object initialState = ManageIntegrationTests.HandleCommand(stateParams);
            
            string initialJson = Newtonsoft.Json.JsonConvert.SerializeObject(initialState);
            JObject initialObj = JObject.Parse(initialJson);
            
            Assert.That(initialObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Should get initial state successfully");
            JObject initialData = initialObj["data"] as JObject;
            
            // State should be Clean initially
            string initialTestState = initialData["currentTestState"]?.ToString();
            Debug.Log($"Initial state: {initialTestState}");
            
            // Try to setup with DummyIntegrationTest (if it exists)
            JObject setupParams = new JObject 
            { 
                ["action"] = "setup",
                ["harnassClass"] = "DummyIntegrationTest"
            };
            
            object setupResult = ManageIntegrationTests.HandleCommand(setupParams);
            string setupJson = Newtonsoft.Json.JsonConvert.SerializeObject(setupResult);
            JObject setupObj = JObject.Parse(setupJson);
            
            // Setup might fail if DummyIntegrationTest doesn't exist, but the command should still be handled properly
            Assert.That(setupObj["success"], Is.Not.Null, "Setup should return success status");
            
            if (setupObj["success"]?.ToObject<bool>() == true)
            {
                Debug.Log("Setup initiated successfully - would normally poll here for completion");
                
                // Get state after setup initiation
                object postSetupState = ManageIntegrationTests.HandleCommand(stateParams);
                string postSetupJson = Newtonsoft.Json.JsonConvert.SerializeObject(postSetupState);
                JObject postSetupObj = JObject.Parse(postSetupJson);
                
                JObject postSetupData = postSetupObj["data"] as JObject;
                string postSetupTestState = postSetupData["currentTestState"]?.ToString();
                Debug.Log($"Post-setup state: {postSetupTestState}");
                
                // In a real polling scenario, we would wait for state to change from SettingUp to SetUp
                // For now, just verify the state response structure is correct
                Assert.That(postSetupData["currentLoadedHarnass"]?.ToString(), Is.EqualTo("DummyIntegrationTest"), "Should track the harness being set up");
            }
            else
            {
                Debug.Log($"Setup failed as expected (likely no DummyIntegrationTest): {setupObj["error"]}");
                // This is expected if DummyIntegrationTest doesn't exist
            }
        }

        #endregion

        #region Integration with CommandRegistry

        [Test]
        [Description("Tests that ManageIntegrationTests is properly registered in CommandRegistry")]
        public void CommandRegistry_HasManageIntegrationTestsHandler_Registered()
        {
            // Act
            var handler = CommandRegistry.GetHandler("HandleManageIntegrationTests");

            // Assert
            Assert.That(handler, Is.Not.Null, "ManageIntegrationTests should be registered in CommandRegistry");
            
            // Test that the handler works with get_all_harnesses action
            JObject testParams = new JObject { ["action"] = "get_all_harnesses" };
            object result = handler(testParams);
            
            Assert.That(result, Is.Not.Null, "Handler should return a result");
            
            string resultJson = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.That(resultObj["success"], Is.Not.Null, "Handler result should have success field");
            
            // Test that the handler works with get_integration_test_state action
            JObject stateParams = new JObject { ["action"] = "get_integration_test_state" };
            object stateResult = handler(stateParams);
            
            Assert.That(stateResult, Is.Not.Null, "Handler should return a state result");
            
            string stateResultJson = Newtonsoft.Json.JsonConvert.SerializeObject(stateResult);
            JObject stateResultObj = JObject.Parse(stateResultJson);
            
            Assert.That(stateResultObj["success"], Is.Not.Null, "State handler result should have success field");
            Assert.That(stateResultObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "State handler should succeed");
            Assert.That(stateResultObj["data"], Is.Not.Null, "State handler should return data");
        }

        #endregion

        #region Helper Methods for Testing

        /// <summary>
        /// Helper method to validate the structure of a command response.
        /// </summary>
        /// <param name="result">The result object to validate</param>
        /// <param name="expectedSuccess">Expected success status</param>
        private void ValidateCommandResponse(object result, bool expectedSuccess)
        {
            Assert.That(result, Is.Not.Null, "Result should not be null");
            
            string resultJson = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            JObject resultObj = JObject.Parse(resultJson);
            
            Assert.That(resultObj["success"], Is.Not.Null, "Result should have success field");
            Assert.That(resultObj["success"].ToObject<bool>(), Is.EqualTo(expectedSuccess), $"Success should be {expectedSuccess}");
            
            if (expectedSuccess)
            {
                Assert.That(resultObj["message"], Is.Not.Null, "Success result should have message");
            }
            else
            {
                Assert.That(resultObj["error"], Is.Not.Null, "Error result should have error message");
            }
        }

        #endregion
    }
}