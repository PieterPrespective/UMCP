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
        private static List<string> testResultFilePaths;

        /// <summary>
        /// Checks if tests are currently running
        /// </summary>
        public static bool IsRunning()
        {
            return isRunning;
        }

        /// <summary>
        /// Resets the test runner state to allow new test runs
        /// Use this method if the test runner gets stuck in a running state
        /// </summary>
        public static void ResetState()
        {
            var wasRunning = isRunning;
            var wasCompleted = testRunCompleted;
            var previousStep = stepGuid;
            
            isRunning = false;
            testRunCompleted = false;
            currentResults = null;
            currentCallback = null;
            stepGuid = null;
            testResultFilePaths = null;
            
            // Stop any running coroutine
            if (currentCoroutine != null)
            {
                EditorCoroutineUtility.StopCoroutine(currentCoroutine);
                currentCoroutine = null;
            }
            
            // Unregister any lingering callbacks
            TestRunnerAPIForwarderUtility.UnregisterCallbacks();
            
            // Force reset the TestRunnerApi instance
            TestRunnerAPIForwarderUtility.ResetTestRunnerApi();
            
            // Log the reset with detailed state information
            Debug.Log($"[RunTests] Test runner state has been reset. Previous state - Running: {wasRunning}, Completed: {wasCompleted}, Step: {previousStep ?? "null"}");
            
            if (wasRunning)
            {
                Debug.Log($"[RunTests] FORCED_RESET - Previous step: {previousStep}");
            }

            EditorApplication.delayCall += () => 
            {
                // Ensure the editor is responsive after reset
                if (!EditorStateHelper.IsEditorResponsive)
                {
                    Debug.LogWarning("[RunTests] Editor was not responsive after reset, forcing update to ensure state is clean.");
                    EditorApplication.update();
                }
            };

            
        }

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
            testResultFilePaths = new List<string>();
            
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

                    yield return new EditorWaitForSeconds(5f); // Small delay between runs

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
            
            Debug.Log($"[RunTests] Starting test execution for mode: {mode}, Filter: {string.Join(", ", filter ?? new string[0])}, Step: {stepGuid}");
            
            // Execute tests through forwarder
            TestRunnerAPIForwarderUtility.ExecuteTests(mode, filter);

            // Wait for tests to complete by checking the completion flag
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMinutes(10); // 10 minute timeout for test execution
            var lastLogTime = DateTime.Now;
            
            while (!testRunCompleted && (DateTime.Now - startTime) < timeout)
            {
                yield return new EditorWaitForSeconds(0.1f);
                
                // Log status every 10 seconds for debugging
                if ((DateTime.Now - lastLogTime).TotalSeconds > 10)
                {
                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    Debug.Log($"[RunTests] Still waiting for test completion. Elapsed: {elapsed:F1}s, Mode: {mode}, Step: {stepGuid}");
                    lastLogTime = DateTime.Now;
                }
            }
            
            if (!testRunCompleted)
            {
                Debug.LogWarning($"[RunTests] Test execution for mode {mode} timed out after {timeout.TotalMinutes} minutes");
                Debug.LogWarning($"[RunTests] TEST_EXECUTION_TIMEOUT - Step: {stepGuid}");
            }
            else
            {
                yield return new EditorWaitForSeconds(1f);
                Debug.Log($"[RunTests] Test execution completed successfully for mode: {mode}, Step: {stepGuid}");
                
                // Save test results to XML file
                SaveTestResultsToFile(mode);
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

        private static void SaveTestResultsToFile(TestMode mode)
        {
            try
            {
                // Get test results from the forwarder
                var testResult = TestRunnerAPIForwarderUtility.GetLastRunResult();
                if (testResult != null)
                {
                    // Create TestResults directory in project root (one folder up from Assets)
                    var projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
                    var resultsDirectory = System.IO.Path.Combine(projectRoot, "TestResults");
                    if (!System.IO.Directory.Exists(resultsDirectory))
                    {
                        System.IO.Directory.CreateDirectory(resultsDirectory);
                    }
                    
                    // Generate filename with timestamp and mode
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var filename = $"TestResults_{mode}_{stepGuid}_{timestamp}.xml";
                    var filePath = System.IO.Path.Combine(resultsDirectory, filename);
                    
                    // Save results to file
                    TestRunnerAPIForwarderUtility.SaveResultToFile(testResult, filePath);
                    
                    // Log the file path for the server to pick up
                    Debug.Log($"[RunTests] TEST_RESULTS_FILE_PATH: {filePath}");
                    
                    // Store the path for later reference
                    testResultFilePaths.Add(filePath);
                }
                else
                {
                    Debug.LogWarning($"[RunTests] No test results available to save for mode: {mode}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RunTests] Failed to save test results to file: {ex}");
            }
        }

        private static void OnRunFinished()
        {
            Debug.Log($"[RunTests] OnRunFinished callback triggered for step: {stepGuid}");
            
            // Signal completion FIRST to stop any polling loops
            testRunCompleted = true;
            
            // Unregister callbacks through forwarder
            TestRunnerAPIForwarderUtility.UnregisterCallbacks();
            
            // Log all test result file paths BEFORE completion marker for better server-side detection
            if (testResultFilePaths != null && testResultFilePaths.Count > 0)
            {
                Debug.Log($"[RunTests] TEST_RESULTS_FILE_PATHS_ALL: {string.Join(";", testResultFilePaths)}");
            }
            
            // Log completion marker for server-side polling detection (after file paths)
            Debug.Log($"[RunTests] TEST_EXECUTION_COMPLETED - Step: {stepGuid}");
            
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
            isRunning = false;
            
            Debug.Log($"[RunTests] OnRunFinished callback completed for step: {stepGuid}");
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

                // Check if any scenes are marked dirty (unsaved changes)
                var dirtyScenes = new List<string>();
                for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
                {
                    var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                    if (scene.isDirty)
                    {
                        dirtyScenes.Add(scene.name);
                    }
                }
                
                if (dirtyScenes.Count > 0)
                {
                    var sceneList = string.Join(", ", dirtyScenes);
                    return Response.Error($"Cannot run tests with unsaved scene changes. The following scenes have unsaved changes: {sceneList}. Please save or discard changes before running tests to prevent Unity save dialogs from blocking execution.");
                }

                isProcessing = true;
                responseData = null;

                // Run tests with immediate response approach to avoid main thread blocking
                // For long-running test operations, we return success immediately and let tests run in background
                
                // Check if tests are already running
                if (RunTestsUtility.IsRunning())
                {
                    return Response.Error("Tests are already running. Please wait for current test execution to complete.");
                }
                
                // Start test execution without blocking
                RunTestsUtility.RunTestsByParameters(parameters, (result) =>
                {
                    // Only log completion status - detailed results are in the XML file
                    Debug.Log($"[RunTests] Test execution completed. AllSuccess: {result.AllSuccess}, Tests: {result.TestResults?.Count ?? 0}");
                });

                // Return immediate success response in format expected by current MCP server
                return new { 
                    success = true, 
                    message = "Test execution started successfully. Check Unity console for results or use RequestStepLogs to retrieve detailed results.", 
                    status = "running",
                    data = new JObject
                    {
                        ["message"] = "Tests are running in background. Results will appear in Unity console.",
                        ["status"] = "running"
                    }
                };
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