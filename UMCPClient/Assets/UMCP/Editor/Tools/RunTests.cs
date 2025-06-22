using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using UMCP.Editor.Helpers;
using Unity.EditorCoroutines.Editor;

namespace UMCP.Editor.Tools
{
    /// <summary>
    /// Data structure representing test result information
    /// </summary>
    public struct TestResultData
    {
        public string TestName { get; set; }
        public string TestAssembly { get; set; }
        public string TestNamespace { get; set; }
        public string ContainerScript { get; set; }
        public bool Success { get; set; }
        public string FailureMessage { get; set; }
        public string StackTrace { get; set; }
        public double Duration { get; set; }
    }

    /// <summary>
    /// Parameters for the RunTests tool
    /// </summary>
    public struct RunTestsParameters
    {
        public string TestMode { get; set; }
        public string[] Filter { get; set; }
        public bool OutputTestResults { get; set; }
        public bool OutputLogData { get; set; }
    }

    /// <summary>
    /// Result structure for RunTests operation
    /// </summary>
    public struct RunTestsResult
    {
        public bool AllSuccess { get; set; }
        public List<TestResultData> TestResults { get; set; }
        public string LogData { get; set; }
    }

    /// <summary>
    /// Utility class for RunTests functionality
    /// </summary>
    public static class RunTestsUtility
    {
        private static bool isRunning = false;
        private static List<TestResultData> currentResults;
        private static EditorCoroutine currentCoroutine;
        private static Action<RunTestsResult> currentCallback;
        private static string stepGuid;
        private static bool testRunCompleted = false;

        /// <summary>
        /// Runs tests based on the provided parameters
        /// </summary>
        public static void RunTestsByParameters(RunTestsParameters parameters, Action<RunTestsResult> callback)
        {
            if (isRunning)
            {
                callback?.Invoke(new RunTestsResult
                {
                    AllSuccess = false,
                    TestResults = new List<TestResultData>(),
                    LogData = "Tests are already running"
                });
                return;
            }

            isRunning = true;
            testRunCompleted = false;
            currentResults = new List<TestResultData>();
            currentCallback = callback;
            
            // Generate unique step GUID for logging
            stepGuid = $"RunTests_{Guid.NewGuid():N}";
            
            // Start coroutine to handle async test execution
            currentCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(RunTestsCoroutine(parameters));
        }

        private static IEnumerator RunTestsCoroutine(RunTestsParameters parameters)
        {
            // Mark start of new step
            Tools.MarkStartOfNewStep.HandleCommand(new JObject { ["stepName"] = stepGuid });
            
            yield return null;

            try
            {
                // Initialize the test API through forwarder
                TestRunnerAPIForwarderUtility.CreateTestRunnerApi();

                // Register callbacks through forwarder
                TestRunnerAPIForwarderUtility.RegisterCallbacks(OnTestFinished, OnRunFinished);

                // Determine test mode
                TestMode mode = TestMode.EditMode;
                if (parameters.TestMode == "PlayMode")
                {
                    mode = TestMode.PlayMode;
                }
                else if (parameters.TestMode == "All")
                {
                    // For "All", we'll need to run both modes separately
                    yield return RunTestsForMode(TestMode.EditMode, parameters.Filter);
                    yield return RunTestsForMode(TestMode.PlayMode, parameters.Filter);
                }
                else
                {
                    yield return RunTestsForMode(mode, parameters.Filter);
                }
            }
            finally
            {
                isRunning = false;
            }
        }

        private static IEnumerator RunTestsForMode(TestMode mode, string[] filter)
        {
            // Reset completion flag for this run
            testRunCompleted = false;
            
            // Execute tests through forwarder
            TestRunnerAPIForwarderUtility.ExecuteTests(mode, filter);

            // Wait for tests to complete by checking the completion flag
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMinutes(10); // 10 minute timeout for test execution
            
            while (!testRunCompleted && (DateTime.Now - startTime) < timeout)
            {
                yield return new EditorWaitForSeconds(0.1f);
            }
            
            if (!testRunCompleted)
            {
                Debug.LogWarning($"[RunTests] Test execution for mode {mode} timed out after {timeout.TotalMinutes} minutes");
            }
        }

        private static void OnTestFinished(TestRunnerAPIForwarderUtility.ForwardedTestResult result)
        {
            var testResult = new TestResultData
            {
                TestName = result.TestName,
                TestAssembly = result.TestAssembly,
                TestNamespace = result.TestNamespace,
                ContainerScript = result.ContainerScript,
                Success = result.Success,
                FailureMessage = result.FailureMessage,
                StackTrace = result.StackTrace,
                Duration = result.Duration
            };
            
            currentResults.Add(testResult);
        }

        private static void OnRunFinished()
        {
            // Unregister callbacks through forwarder
            TestRunnerAPIForwarderUtility.UnregisterCallbacks();
            
            // Get log data if requested
            string logData = "";
            if (currentCallback != null)
            {
                var result = Tools.RequestStepLogs.HandleCommand(new JObject 
                { 
                    ["stepName"] = stepGuid,
                    ["includeStacktraces"] = true
                });
                
                if (result is JObject logResult && logResult["status"]?.ToString() == "success")
                {
                    logData = logResult["data"]?.ToString() ?? "";
                }
            }
            
            // Prepare final result
            var finalResult = new RunTestsResult
            {
                AllSuccess = currentResults.All(r => r.Success),
                TestResults = new List<TestResultData>(currentResults),
                LogData = logData
            };
            
            currentCallback?.Invoke(finalResult);
            testRunCompleted = true; // Signal completion
            isRunning = false;
        }

    }

    /// <summary>
    /// Handles running tests via Unity Test Runner
    /// </summary>
    public static class RunTests
    {
        private static bool isProcessing = false;
        private static JObject responseData;

        /// <summary>
        /// Main handler for the run_tests command
        /// </summary>
        /// <param name="params">JSON parameters containing test execution settings</param>
        /// <returns>Response object with test results</returns>
        public static object HandleCommand(JObject @params)
        {
            try
            {
                // Parse parameters
                var parameters = new RunTestsParameters
                {
                    TestMode = @params?["TestMode"]?.ToString() ?? "All",
                    Filter = (@params?["Filter"] as JArray)?.Select(t => t.ToString()).ToArray() ?? new string[0],
                    OutputTestResults = @params?["OutputTestResults"]?.Value<bool>() ?? true,
                    OutputLogData = @params?["OutputLogData"]?.Value<bool>() ?? true
                };

                // Validate TestMode
                if (!IsValidTestMode(parameters.TestMode))
                {
                    return Response.Error($"Invalid TestMode: '{parameters.TestMode}'. Valid values are 'EditMode', 'PlayMode', or 'All'.");
                }

                // Check if Unity is in a state where tests can run
                if (!EditorStateHelper.CanModifyProjectFiles)
                {
                    return Response.Error("Cannot run tests while Unity is compiling or in play mode.");
                }

                isProcessing = true;
                responseData = null;

                // Run tests asynchronously
                RunTestsUtility.RunTestsByParameters(parameters, (result) =>
                {
                    // Build response
                    var response = new JObject
                    {
                        ["AllSuccess"] = result.AllSuccess
                    };

                    if (parameters.OutputTestResults)
                    {
                        var testResults = new JArray();
                        foreach (var test in result.TestResults)
                        {
                            var testObj = new JObject
                            {
                                ["TestName"] = test.TestName,
                                ["TestAssembly"] = test.TestAssembly,
                                ["TestNamespace"] = test.TestNamespace,
                                ["ContainerScript"] = test.ContainerScript,
                                ["Success"] = test.Success,
                                ["Duration"] = test.Duration
                            };
                            
                            // Include failure details if test failed
                            if (!test.Success)
                            {
                                testObj["FailureMessage"] = test.FailureMessage;
                                testObj["StackTrace"] = test.StackTrace;
                            }
                            
                            testResults.Add(testObj);
                        }
                        response["TestResults"] = testResults;
                    }

                    if (parameters.OutputLogData)
                    {
                        response["LogData"] = result.LogData;
                    }

                    responseData = response;
                    isProcessing = false;
                });

                // Wait for completion (synchronous response required by bridge)
                var startTime = DateTime.Now;
                var timeout = TimeSpan.FromMinutes(5); // 5 minute timeout for test execution

                while (isProcessing && (DateTime.Now - startTime) < timeout)
                {
                    System.Threading.Thread.Sleep(100);
                }

                if (isProcessing)
                {
                    return Response.Error("Test execution timed out after 5 minutes.");
                }

                return Response.Success("Tests completed.", responseData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RunTests] Error running tests: {e}");
                return Response.Error($"Failed to run tests: {e.Message}");
            }
            finally
            {
                isProcessing = false;
            }
        }

        /// <summary>
        /// Validates if the provided test mode is valid
        /// </summary>
        private static bool IsValidTestMode(string testMode)
        {
            return testMode == "EditMode" || testMode == "PlayMode" || testMode == "All";
        }
    }
}