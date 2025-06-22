using UnityEngine;
using System;
using System.Collections.Generic;

namespace UnityEditor.TestTools.TestRunner.Api
{
    /// <summary>
    /// Forwarder utility to handle internal ITestRunnerApi operations
    /// This class has access to internal types through assembly reference
    /// </summary>
    public static class TestRunnerAPIForwarderUtility
    {
        /// <summary>
        /// Data structure to pass test result information without using internal types
        /// </summary>
        public struct ForwardedTestResult
        {
            public string TestName;
            public string TestAssembly;
            public string TestNamespace;
            public string ContainerScript;
            public bool Success;
            public string FailureMessage;
            public string StackTrace;
            public double Duration;
        }

        /// <summary>
        /// Callback delegate for test completion
        /// </summary>
        public delegate void TestFinishedCallback(ForwardedTestResult result);

        /// <summary>
        /// Callback delegate for test run completion
        /// </summary>
        public delegate void RunFinishedCallback();

        private static TestRunnerApi testApi;
        private static TestCallbackForwarder currentCallbacks;

        /// <summary>
        /// Creates and returns a TestRunnerApi instance
        /// </summary>
        public static object CreateTestRunnerApi()
        {
            if (testApi == null)
            {
                testApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            }
            return testApi;
        }

        /// <summary>
        /// Registers callbacks for test execution
        /// </summary>
        public static void RegisterCallbacks(TestFinishedCallback onTestFinished, RunFinishedCallback onRunFinished)
        {
            if (testApi == null)
            {
                CreateTestRunnerApi();
            }

            currentCallbacks = new TestCallbackForwarder(onTestFinished, onRunFinished);
            testApi.RegisterCallbacks(currentCallbacks);
        }

        /// <summary>
        /// Unregisters callbacks
        /// </summary>
        public static void UnregisterCallbacks()
        {
            if (testApi != null && currentCallbacks != null)
            {
                testApi.UnregisterCallbacks(currentCallbacks);
            }
        }

        /// <summary>
        /// Retrieves the test list for a specific test mode asynchronously
        /// </summary>
        public static void RetrieveTestList(TestMode mode, Action<ITestAdaptor> callback)
        {
            if (testApi == null)
            {
                CreateTestRunnerApi();
            }

            testApi.RetrieveTestList(mode, callback);
        }

        /// <summary>
        /// Executes tests with the provided settings
        /// </summary>
        public static void ExecuteTests(TestMode mode, string[] testNames = null)
        {
            if (testApi == null)
            {
                CreateTestRunnerApi();
            }

            var executionSettings = new ExecutionSettings()
            {
                runSynchronously = false,
                targetPlatform = null
            };
            
            if (testNames != null && testNames.Length > 0)
            {
                var testFilter = new Filter()
                {
                    testMode = mode,
                    testNames = testNames
                };
                executionSettings.filters = new[] { testFilter };
            }
            else
            {
                var testFilter = new Filter()
                {
                    testMode = mode
                };
                executionSettings.filters = new[] { testFilter };
            }

            testApi.Execute(executionSettings);
        }

        /// <summary>
        /// Internal callback forwarder that converts internal types to public ones
        /// </summary>
        private class TestCallbackForwarder : ICallbacks
        {
            private readonly TestFinishedCallback onTestFinished;
            private readonly RunFinishedCallback onRunFinished;

            public TestCallbackForwarder(TestFinishedCallback testFinished, RunFinishedCallback runFinished)
            {
                onTestFinished = testFinished;
                onRunFinished = runFinished;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                // Not used in current implementation
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                onRunFinished?.Invoke();
            }

            public void TestStarted(ITestAdaptor test)
            {
                // Not used in current implementation
            }
            

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.Method != null && !result.Test.IsSuite)
                {
                    var forwardedResult = new ForwardedTestResult
                    {
                        TestName = result.Test.FullName,
                        TestAssembly = ExtractAssembly(result.Test.FullName),
                        TestNamespace = ExtractNamespace(result.Test.FullName),
                        ContainerScript = ExtractContainerScript(result.Test.FullName),
                        Success = result.TestStatus == TestStatus.Passed,
                        FailureMessage = result.Message ?? string.Empty,
                        StackTrace = result.StackTrace ?? string.Empty,
                        Duration = result.Duration
                    };

                    // Log failure details to console
                    if (result.TestStatus == TestStatus.Failed)
                    {
                        UnityEngine.Debug.LogError($"[Test Failed] {result.Test.FullName}\n{result.Message}\n{result.StackTrace}");
                    }

                    onTestFinished?.Invoke(forwardedResult);
                }
            }

            private static string ExtractAssembly(string fullName)
            {
                var parts = fullName.Split('.');
                return parts.Length > 0 ? parts[0] : "Unknown";
            }

            private static string ExtractNamespace(string fullName)
            {
                var lastDotIndex = fullName.LastIndexOf('.');
                if (lastDotIndex > 0)
                {
                    var nameWithoutMethod = fullName.Substring(0, lastDotIndex);
                    var secondLastDotIndex = nameWithoutMethod.LastIndexOf('.');
                    if (secondLastDotIndex > 0)
                    {
                        return nameWithoutMethod.Substring(0, secondLastDotIndex);
                    }
                }
                return string.Empty;
            }

            private static string ExtractContainerScript(string fullName)
            {
                var parts = fullName.Split('.');
                if (parts.Length >= 2)
                {
                    return parts[parts.Length - 2];
                }
                return string.Empty;
            }
        }
    }
}
