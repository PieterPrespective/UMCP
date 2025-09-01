using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UMCP.Editor.Helpers;
using UMCP.Editor.Tools;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Events;

namespace UMCP.Editor
{
    /// <summary>
    /// Scriptable object that represents the state of the current running testset
    /// (survives a domain reload)
    /// </summary>
    [Serializable]
    public class RunTestsState : ScriptableObject
    {
        /// <summary>
        /// GUID of the test to run
        /// </summary>
        public string RunTestsGUID;

        /// <summary>
        /// Starttime of the test
        /// </summary>
        public string RunTestsStartTime;

        /// <summary>
        /// Paths containing the results of the tests
        /// </summary>
        public List<string> TestResultFilePaths = new List<string>();

        /// <summary>
        /// Unity Tester modes in which to run before the test run is complete
        /// </summary>
        public List<TestMode> TestModes;

        /// <summary>
        /// Results storage for client-side debugging purposes (not used for anything else currently)
        /// </summary>
        public List<TestResultData> CurrentResults;

        /// <summary>
        /// Whether the test runner is (or should be) running
        /// </summary>
        public bool IsRunning = false;

        /// <summary>
        /// Whether the test set has been completed
        /// </summary>
        public bool IsCompleted = false;

        /// <summary>
        /// Callback invoked when the testset completes
        /// </summary>
        public string CompletionCallback = null;

        /// <summary>
        /// Filter applied during testing: (partial) names and/or namespaces to include
        /// </summary>
        public string[] Filter;
    }

    /// <summary>
    /// Utility for the RunTestTool State system
    /// </summary>
    [InitializeOnLoad]
    public class RunTestsStateUtility
    {
        /// <summary>
        /// Actually start running a test by parameters
        /// </summary>
        /// <param name="parameters">parameters of the testrun</param>
        /// <param name="failMessage">message indicating why starting the testrun failed</param>
        /// <returns></returns>
        public static bool RunTestsByParameters(RunTestsParameters parameters, out string guid, out string failMessage, string _callbackFunction = null)
        {
            failMessage = null;
            guid = null;
            if (IsRunningTests())
            {
                failMessage = $"Cannot start new test run while another (GUID: {StateStorage.RunTestsGUID}, Started at:{StateStorage.RunTestsStartTime.ToString()}) is in progress.";
                return false;
            }
            // Clear any previous state
            ClearState();

            List<TestMode> testModes = new List<TestMode>();
            switch(parameters.TestMode)
                {
                case "EditMode":
                    testModes = new List<TestMode>() { TestMode.EditMode };
                    break;
                case "PlayMode":
                    testModes = new List<TestMode>() { TestMode.PlayMode };
                    break;
                case "All":
                    testModes = new List<TestMode>() { TestMode.EditMode, TestMode.PlayMode };
                    break;
                default:
                    failMessage = $"Invalid TestMode: {parameters.TestMode}, expected 'EditMode', 'PlayMode' or 'All'";
                    return false;
            }


            // Initialize state storage
            StateStorage.RunTestsGUID = $"RunTests_{Guid.NewGuid():N}"; 
            guid = StateStorage.RunTestsGUID;
            StateStorage.RunTestsStartTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            StateStorage.TestModes = testModes;
            StateStorage.IsCompleted = false;
            StateStorage.CurrentResults = new List<TestResultData>();
            StateStorage.CompletionCallback = _callbackFunction;
            StateStorage.Filter = parameters.Filter ?? Array.Empty<string>();
            StartNextModeTests();
            return true;
        }

        /// <summary>
        /// Within a running testset - start running a next mode
        /// </summary>
        private static void StartNextModeTests()
        {
            if(!StateStorage.IsRunning)
            {
                Tools.MarkStartOfNewStep.HandleCommand(new JObject { ["stepName"] = StateStorage.RunTestsGUID });
                StateStorage.IsRunning = true;
            }
            
            try
            {
                // Initialize the test API through forwarder
                TestRunnerAPIForwarderUtility.CreateTestRunnerApi();

                // Register callbacks through forwarder
                TestRunnerAPIForwarderUtility.RegisterCallbacks(OnTestFinished, OnRunFinished);

                TestMode nextMode = StateStorage.TestModes[0];
                TestRunnerAPIForwarderUtility.ExecuteTests(nextMode, StateStorage.Filter);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RunTests] Error starting tests: {ex.Message}");
                StateStorage.IsRunning = false;
                StateStorage.IsCompleted = true;
                return;
            }

        }

        /// <summary>
        /// Clear any existing state for the runtest tool
        /// </summary>
        public static void ClearState()
        {
            RunTestsState[] runTestsStates = Resources.FindObjectsOfTypeAll<RunTestsState>();
            for(int i = 0; i < runTestsStates.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(runTestsStates[i]);
            }
            _stateStorage = null;
        }

        /// <summary>
        /// Returns whether any tests shoul be running at this time
        /// </summary>
        /// <returns></returns>
        public static bool IsRunningTests()
        {
            RunTestsState[] runTestsStates = Resources.FindObjectsOfTypeAll<RunTestsState>();
            if (runTestsStates.Length > 0)
            {
                //If something went wrong we still may have a residual state object - check whether it isn't invalid
                // - If the state isn't running its not valid
                // - If the state is completed its not valid
                // - If no valid GUID is present its not valid
                return !(!runTestsStates[0].IsRunning || runTestsStates[0].IsCompleted || string.IsNullOrEmpty(runTestsStates[0].RunTestsGUID));
            }
            return false;
        }

        /// <summary>
        /// After a domain relad:
        /// - If we have a residual state storage; clear it
        /// - If we have running tests, re-register the required callbacks
        /// </summary>
        static RunTestsStateUtility()
        {
            if(Resources.FindObjectsOfTypeAll<RunTestsState>().Length > 0)
            {
                if(!IsRunningTests())
                    {
                    Debug.LogWarning("[RunTests] RunTestsState already completed, clearing state.");
                    ClearState();
                }
                else
                {
                    Debug.LogWarning("[RunTests] Recompile midway a test - re-registering callbacks");
                    TestRunnerAPIForwarderUtility.RegisterCallbacks(OnTestFinished, OnRunFinished);
                }
            }
        }

        /// <summary>
        /// Invoked when a test finishes - not currently used
        /// </summary>
        /// <param name="result">result of the finished test</param>
        public static void OnTestFinished(TestRunnerAPIForwarderUtility.ForwardedTestResult result)
        {
            if(!IsRunningTests())
                {
                Debug.LogWarning("[RunTests] OnTestFinished called but no tests are running.");
                return;
                }


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

            StateStorage.CurrentResults.Add(testResult);
        }

        /// <summary>
        /// After completing Unity Testrunner completes a run, store the results to a file (and store which file was created)
        /// </summary>
        /// <param name="mode">Finished state of the testrunner</param>
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
                    var filename = $"TestResults_{mode}_{StateStorage.RunTestsGUID}_{timestamp}.xml";
                    var filePath = System.IO.Path.Combine(resultsDirectory, filename);

                    // Save results to file
                    TestRunnerAPIForwarderUtility.SaveResultToFile(testResult, filePath);

                    // Log the file path for the server to pick up
                    Debug.Log($"[RunTests] TEST_RESULTS_FILE_PATH: {filePath}");

                    // Store the path for later reference
                    StateStorage.TestResultFilePaths.Add(filePath);
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


        /// <summary>
        /// Invoked after the Unity Testrunner completes a run (if set to mode 'all' this happens twice)
        /// </summary>
        public static void OnRunFinished()
        {
            if(!IsRunningTests())
                {
                Debug.LogWarning("[RunTests] OnRunFinished called but no tests are running.");
                return;
                }

            Debug.Log($"[RunTests] OnRunFinished callback triggered for step: {StateStorage.RunTestsGUID}");

            SaveTestResultsToFile(StateStorage.TestModes[0]);
            StateStorage.TestModes.RemoveAt(0);

            if (StateStorage.TestModes.Count > 0)
            {
                // If there are more modes to run, start the next one
                Debug.Log($"[RunTests] Starting next test mode: {StateStorage.TestModes[0]}");
                StartNextModeTests();
                return;
            }

            // Signal completion FIRST to stop any polling loops
            StateStorage.IsCompleted = true;

            // Unregister callbacks through forwarder
            TestRunnerAPIForwarderUtility.UnregisterCallbacks();

            // Log all test result file paths BEFORE completion marker for better server-side detection
            if (StateStorage.TestResultFilePaths != null && StateStorage.TestResultFilePaths.Count > 0)
            {
                Debug.Log($"[RunTests] TEST_RESULTS_FILE_PATHS_ALL: [' {string.Join("',''", StateStorage.TestResultFilePaths)}']");
            }

            string testStart = StateStorage.RunTestsStartTime;
            string testEnd = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            //Result counting is not trustworthy - tests that cross the domain reload boundary are not reported properly
            //int totalTests = StateStorage.CurrentResults.Count;
            //int totalSuccess = StateStorage.CurrentResults.Count(r => r.Success);
            //int totalFailures = totalTests - totalSuccess;

            // Log completion marker for server-side polling detection (after file paths)
            Debug.Log("[RunTests] TEST_EXECUTION_COMPLETED - {" + $"GUID: {StateStorage.RunTestsGUID}, TestStart:{testStart}, TestEnd:{testEnd}, TestResultPaths:[' {string.Join("',''", StateStorage.TestResultFilePaths)}']" + "}");

            // Get log data if requested
            string logData = "";
            if (!string.IsNullOrEmpty(StateStorage.CompletionCallback))
            {
                var result = Tools.RequestStepLogs.HandleCommand(new JObject
                {
                    ["stepName"] = StateStorage.RunTestsGUID,
                    ["includeStacktraces"] = true
                });

                if (result is JObject logResult && logResult["status"]?.ToString() == "success")
                {
                    logData = logResult["data"]?.ToString() ?? "";
                }



                string[] typenameAndFunction = StateStorage.CompletionCallback.Split(new char[] { '.' }, 2);
                if (typenameAndFunction.Length == 2)
                {
                    string typeName = typenameAndFunction[0];
                    string functionName = typenameAndFunction[1];
                    Type callbackType = Type.GetType(typeName);
                    if (callbackType != null)
                    {
                        var methodInfo = callbackType.GetMethod(functionName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        if (methodInfo != null)
                        {
                            try
                            {
                                //Prepare final result
                                var finalResult = new RunTestsResult
                                {
                                    AllSuccess = StateStorage.CurrentResults.All(r => r.Success),
                                    TestResults = new List<TestResultData>(StateStorage.CurrentResults),
                                    LogData = logData
                                };

                                methodInfo.Invoke(null, new object[] { finalResult });
                                Debug.Log($"[RunTests] Invoked completion callback: {StateStorage.CompletionCallback}");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"[RunTests] Error invoking completion callback '{StateStorage.CompletionCallback}': {ex.Message}");
                            }
                        }
                        else
                        {
                            Debug.LogError($"[RunTests] Method '{functionName}' not found in type '{typeName}' for completion callback.");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[RunTests] Type '{typeName}' not found for completion callback.");
                    }
                }
                else
                {
                    Debug.LogError($"[RunTests] Invalid CompletionCallback format: '{StateStorage.CompletionCallback}'. Expected 'Namespace.TypeName.MethodName'.");
                }

            }

            Debug.Log($"[RunTests] OnRunFinished callback completed for step: {StateStorage.RunTestsGUID}");

            ClearState(); 
        }



        /// <summary>
        /// Reference to the current test state storage (used because it survives a domain reload)
        /// </summary>

        private static RunTestsState _stateStorage;
        public static RunTestsState StateStorage
        {
            get
            {
                if (_stateStorage == null)
                {
                    // Try to find existing storage
                    var storages = Resources.FindObjectsOfTypeAll<RunTestsState>();
                    if (storages.Length > 0)
                    {
                        _stateStorage = storages[0];
                    }
                    else
                    {
                        // Create new storage
                        _stateStorage = ScriptableObject.CreateInstance<RunTestsState>();
                        _stateStorage.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
                return _stateStorage;
            }
        }
    }
}
