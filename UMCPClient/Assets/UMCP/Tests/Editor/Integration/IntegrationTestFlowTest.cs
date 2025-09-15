using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Newtonsoft.Json.Linq;
using UMCP.Editor.Tools;

namespace UMCP.Tests.Editor.Integration
{
    /// <summary>
    /// End-to-end test for the integration test flow.
    /// Tests the complete lifecycle of setting up, running, and cleaning up integration tests.
    /// Uses polling architecture to monitor state transitions instead of arbitrary waits.
    /// </summary>
    public class IntegrationTestFlowTest
    {
        [UnityTest]
        [Description("Tests the complete integration test flow end-to-end with polling for state transitions")]
        public IEnumerator TestCompleteIntegrationFlow()
        {
            Debug.Log("=== Starting Integration Test Flow E2E Test ===");

            // Step 1: Validate harnesses (should find DummyIntegrationTest)
            Debug.Log("Step 1: Validating harnesses...");
            JObject getAllHarnessParams = new JObject { ["action"] = "get_all_harnesses" };
            object validateResult = ManageIntegrationTests.HandleCommandSynchronous(getAllHarnessParams);
            
            string validateJson = Newtonsoft.Json.JsonConvert.SerializeObject(validateResult);
            JObject validateObj = JObject.Parse(validateJson);
            Debug.Log($"Validation result: {validateJson}");
            
            Assert.That(validateObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Harness Retrieval should succeed");
            
            JObject validateData = validateObj["data"] as JObject;
            Assert.That(validateData["totalHarnesses"], Is.Not.Null, "Should report total harness count");
            
            int totalHarnesses = validateData["totalHarnesses"].ToObject<int>();
            Assert.That(totalHarnesses, Is.GreaterThan(0), "Should find at least one harness");
            
            Debug.Log($"Found {totalHarnesses} integration test harnesses");

            yield return new WaitForSeconds(0.5f);

            // Step 2: Setup integration test with DummyIntegrationTest
            Debug.Log(">>> Step 2: Setting up DummyIntegrationTest...");
            JObject setupParams = new JObject 
            { 
                ["action"] = "setup",
                ["harnassClass"] = "DummyIntegrationTest"
            };
            
            object setupResult = ManageIntegrationTests.HandleCommandSynchronous(setupParams);
            string setupJson = Newtonsoft.Json.JsonConvert.SerializeObject(setupResult);
            JObject setupObj = JObject.Parse(setupJson);
            Debug.Log($"Setup result: {setupJson}");
            
            Assert.That(setupObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Integration test setup should succeed");
            
            JObject setupData = setupObj["data"] as JObject;
            Assert.That(setupData["harnassClass"]?.ToString(), Is.EqualTo("DummyIntegrationTest"), "Should confirm harness class");
            Assert.That(setupData["state"]?.ToString(), Is.EqualTo("SettingUp"), "Should be in SettingUp state initially");

            // Poll for setup completion instead of arbitrary wait
            Debug.Log(">>> Polling for setup completion...");
            JObject stateParams = new JObject { ["action"] = "get_integration_test_state" };
            
            bool setupCompleted = false;
            float maxWaitTime = 10.0f; // Maximum wait time
            float elapsedTime = 0.0f;
            float pollInterval = 0.5f;
            
            while (!setupCompleted && elapsedTime < maxWaitTime)
            {
                yield return new WaitForSeconds(pollInterval);
                elapsedTime += pollInterval;
                
                object stateResult = ManageIntegrationTests.HandleCommandSynchronous(stateParams);
                string stateJson = Newtonsoft.Json.JsonConvert.SerializeObject(stateResult);
                JObject stateObj = JObject.Parse(stateJson);
                
                if (stateObj["success"]?.ToObject<bool>() == true)
                {
                    JObject stateData = stateObj["data"] as JObject;
                    string currentState = stateData["currentTestState"]?.ToString();
                    Debug.Log($"Polling: Current state = {currentState} (elapsed: {elapsedTime:F1}s)");
                    
                    if (currentState == "SetUp")
                    {
                        setupCompleted = true;
                        Debug.Log(">>> Setup completed successfully!");
                    }
                    else if (currentState == "Clean")
                    {
                        // Setup failed and was cleaned up
                        string lastError = stateData["lastError"]?.ToString();
                        Assert.Fail($"Setup failed and returned to Clean state. Error: {lastError}");
                    }
                }
            }
            
            Assert.That(setupCompleted, Is.True, $"Setup should complete within {maxWaitTime} seconds");

            // Step 3: Verify the expected log message was created
            Debug.Log(">>> Step 3: Verifying console logs...");
            
            // Use ReadConsole to check for the expected log message
            JObject consoleParams = new JObject
            {
                ["action"] = "get",
                ["filterText"] = "DummyIntegrationTest setup completed successfully",
                ["count"] = 10
            };
            
            object consoleResult = ReadConsole.HandleCommandSynchronous(consoleParams);
            string consoleJson = Newtonsoft.Json.JsonConvert.SerializeObject(consoleResult);
            JObject consoleObj = JObject.Parse(consoleJson);
            Debug.Log($"Console result: {consoleJson}");
            
            Assert.That(consoleObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Console read should succeed");
            
            JArray logEntries = consoleObj["data"] as JArray;
            Assert.That(logEntries, Is.Not.Null, "Should return log entries array");
            Assert.That(logEntries.Count, Is.GreaterThan(0), "Should find the expected log message");
            
            // Verify we found the expected message
            bool foundExpectedMessage = false;
            foreach (JObject entry in logEntries)
            {
                string message = entry["message"]?.ToString();
                if (message != null && message.Contains("DummyIntegrationTest setup completed successfully"))
                {
                    foundExpectedMessage = true;
                    Debug.Log($"Found expected log message: {message}");
                    break;
                }
            }
            
            Assert.That(foundExpectedMessage, Is.True, "Should find the expected setup completion message");

            yield return new WaitForSeconds(0.5f);

            // Step 4: Cleanup integration test
            Debug.Log("Step 4: Cleaning up integration test...");
            JObject cleanupParams = new JObject { ["action"] = "cleanup" };
            
            object cleanupResult = ManageIntegrationTests.HandleCommandSynchronous(cleanupParams);
            string cleanupJson = Newtonsoft.Json.JsonConvert.SerializeObject(cleanupResult);
            JObject cleanupObj = JObject.Parse(cleanupJson);
            Debug.Log($"Cleanup result: {cleanupJson}");
            
            Assert.That(cleanupObj["success"]?.ToObject<bool>(), Is.EqualTo(true), "Integration test cleanup should succeed");

            // Poll for cleanup completion instead of arbitrary wait
            Debug.Log(">>> Polling for cleanup completion...");
            
            bool cleanupCompleted = false;
            elapsedTime = 0.0f;
            
            while (!cleanupCompleted && elapsedTime < maxWaitTime)
            {
                yield return new WaitForSeconds(pollInterval);
                elapsedTime += pollInterval;
                
                object stateResult = ManageIntegrationTests.HandleCommandSynchronous(stateParams);
                string stateJson = Newtonsoft.Json.JsonConvert.SerializeObject(stateResult);
                JObject stateObj = JObject.Parse(stateJson);
                
                if (stateObj["success"]?.ToObject<bool>() == true)
                {
                    JObject stateData = stateObj["data"] as JObject;
                    string currentState = stateData["currentTestState"]?.ToString();
                    Debug.Log($"Polling cleanup: Current state = {currentState} (elapsed: {elapsedTime:F1}s)");
                    
                    if (currentState == "Clean")
                    {
                        cleanupCompleted = true;
                        Debug.Log(">>> Cleanup completed successfully!");
                    }
                }
            }
            
            Assert.That(cleanupCompleted, Is.True, $"Cleanup should complete within {maxWaitTime} seconds");

            Debug.Log("=== Integration Test Flow E2E Test Completed Successfully ===");
        }

        [Test]
        [Description("Tests that the ManageIntegrationTests tool is properly registered")]
        public void TestToolRegistration()
        {
            Debug.Log("Testing ManageIntegrationTests tool registration...");
            
            // Verify the tool is registered in CommandRegistry
            var handler = CommandRegistry.GetHandler("HandleManageIntegrationTests");
            Assert.That(handler, Is.Not.Null, "ManageIntegrationTests should be registered in CommandRegistry");
            
            // Test a basic command
            JObject testParams = new JObject { ["action"] = "validate_harnesses" };
            object result = handler(testParams);
            Assert.That(result, Is.Not.Null, "Handler should return a result");
            
            Debug.Log("ManageIntegrationTests tool registration test passed");
        }
    }
}