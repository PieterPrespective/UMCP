using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using UMCP.Editor.Helpers;

namespace UMCP.Editor.Tools
{
    /// <summary>
    /// Data structure representing test information
    /// </summary>
    public struct TestInfo
    {
        public string TestName { get; set; }
        public string TestAssembly { get; set; }
        public string TestNamespace { get; set; }
        public string ContainerScript { get; set; }
    }

    /// <summary>
    /// Parameters for the GetTests tool
    /// </summary>
    public struct GetTestsParameters
    {
        public string TestMode { get; set; }
        public string Filter { get; set; }
    }

    /// <summary>
    /// Utility class for GetTests functionality
    /// </summary>
    public static class GetTestsUtility
    {
        /// <summary>
        /// Retrieves test information based on the provided parameters using coroutine
        /// </summary>
        /// <param name="parameters">The parameters specifying which tests to retrieve</param>
        /// <param name="callback">Callback to invoke when tests are retrieved</param>
        public static void GetTestsByParametersAsync(GetTestsParameters parameters, Action<List<TestInfo>> callback)
        {
            // Start the coroutine without an owner (this should work in Editor context)
            EditorCoroutineUtility.StartCoroutineOwnerless(RetrieveTestsCoroutine(parameters, callback));
        }
        
        /// <summary>
        /// Coroutine to retrieve tests asynchronously
        /// </summary>
        private static IEnumerator RetrieveTestsCoroutine(GetTestsParameters parameters, Action<List<TestInfo>> callback)
        {
            yield return null; // Ensure we yield at least once at the beginning
            
            var tests = new List<TestInfo>();
            
            // Initialize the test API through forwarder
            TestRunnerAPIForwarderUtility.CreateTestRunnerApi();
            
            // Determine which test modes to query
            bool includeEditMode = parameters.TestMode == "EditMode" || parameters.TestMode == "All";
            bool includePlayMode = parameters.TestMode == "PlayMode" || parameters.TestMode == "All";
            
            if (includeEditMode)
            {
                bool editModeCompleted = false;
                
                TestRunnerAPIForwarderUtility.RetrieveTestList(TestMode.EditMode, (testList) =>
                {
                    if (testList != null)
                    {
                        AddTestsToList(testList, tests, parameters.Filter);
                    }
                    editModeCompleted = true;
                });
                
                // Wait for EditMode callback
                var timeout = 0f;
                while (!editModeCompleted && timeout < 10f)
                {
                    timeout += 0.1f;
                    yield return new EditorWaitForSeconds(0.1f);
                }
                
                if (!editModeCompleted)
                {
                    Debug.LogWarning("[GetTests] EditMode retrieval timed out");
                }
            }
            
            if (includePlayMode)
            {
                bool playModeCompleted = false;
                
                TestRunnerAPIForwarderUtility.RetrieveTestList(TestMode.PlayMode, (testList) =>
                {
                    if (testList != null)
                    {
                        AddTestsToList(testList, tests, parameters.Filter);
                    }
                    playModeCompleted = true;
                });
                
                // Wait for PlayMode callback
                var timeout = 0f;
                while (!playModeCompleted && timeout < 10f)
                {
                    timeout += 0.1f;
                    yield return new EditorWaitForSeconds(0.1f);
                }
                
                if (!playModeCompleted)
                {
                    Debug.LogWarning("[GetTests] PlayMode retrieval timed out");
                }
            }
            
            callback?.Invoke(tests);
        }
        
        /// <summary>
        /// Synchronous version that properly waits for async operation without blocking Unity main thread
        /// </summary>
        public static List<TestInfo> GetTestsByParameters(GetTestsParameters parameters)
        {
            var tests = new List<TestInfo>();
            bool completed = false;
            Exception error = null;
            
            // Start async operation
            GetTestsByParametersAsync(parameters, (result) =>
            {
                tests = result;
                completed = true;
            });
            
            // Wait for completion using cooperative yield to avoid blocking Unity
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromSeconds(15);
            
            while (!completed && (DateTime.Now - startTime) < timeout)
            {
                // Use a small delay to prevent tight loop, but yield thread
                System.Threading.Tasks.Task.Delay(10).Wait();
                
                // Allow other threads to run
                System.Threading.Thread.Yield();
            }
            
            if (!completed)
            {
                Debug.LogWarning("[GetTests] Synchronous operation timed out after 15 seconds");
            }
            
            return tests;
        }
        
        /// <summary>
        /// Recursively adds tests from the test tree to the list
        /// </summary>
        /// <param name="testNode">The root test node</param>
        /// <param name="tests">The list to add tests to</param>
        /// <param name="filter">Optional filter string</param>
        private static void AddTestsToList(ITestAdaptor testNode, List<TestInfo> tests, string filter)
        {
            if (testNode == null) return;
            
            // Process children first (test fixtures and test methods)
            if (testNode.Children != null)
            {
                foreach (var child in testNode.Children)
                {
                    AddTestsToList(child, tests, filter);
                }
            }
            
            // Only add actual test methods (not test fixtures/suites)
            if (testNode.Method != null && !testNode.IsSuite)
            {
                var testInfo = new TestInfo
                {
                    TestName = testNode.FullName,
                    TestAssembly = testNode.TestCaseCount > 0 ? testNode.FullName.Split('.')[0] : "Unknown",
                    TestNamespace = ExtractNamespace(testNode.FullName),
                    ContainerScript = ExtractContainerScript(testNode.FullName)
                };
                
                // Apply filter if specified
                if (string.IsNullOrEmpty(filter) || 
                    testInfo.TestName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    testInfo.TestNamespace.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    tests.Add(testInfo);
                }
            }
        }
        
        /// <summary>
        /// Extracts the namespace from a full test name
        /// </summary>
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
        
        /// <summary>
        /// Extracts the container script name from a full test name
        /// </summary>
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

    /// <summary>
    /// Handles retrieving test information from Unity Test Runner
    /// </summary>
    public static class GetTests
    {
        /// <summary>
        /// Recursively adds tests from the test tree to the list
        /// </summary>
        /// <param name="testNode">The root test node</param>
        /// <param name="tests">The list to add tests to</param>
        /// <param name="filter">Optional filter string</param>
        private static void AddTestsToList(ITestAdaptor testNode, List<TestInfo> tests, string filter)
        {
            if (testNode == null) return;
            
            // Process children first (test fixtures and test methods)
            if (testNode.Children != null)
            {
                foreach (var child in testNode.Children)
                {
                    AddTestsToList(child, tests, filter);
                }
            }
            
            // Only add actual test methods (not test fixtures/suites)
            if (testNode.Method != null && !testNode.IsSuite)
            {
                var testInfo = new TestInfo
                {
                    TestName = testNode.FullName,
                    TestAssembly = testNode.TestCaseCount > 0 ? testNode.FullName.Split('.')[0] : "Unknown",
                    TestNamespace = ExtractNamespace(testNode.FullName),
                    ContainerScript = ExtractContainerScript(testNode.FullName)
                };
                
                // Apply filter if specified
                if (string.IsNullOrEmpty(filter) || 
                    testInfo.TestName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    testInfo.TestNamespace.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    tests.Add(testInfo);
                }
            }
        }
        
        /// <summary>
        /// Extracts the namespace from a full test name
        /// </summary>
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
        
        /// <summary>
        /// Extracts the container script name from a full test name
        /// </summary>
        private static string ExtractContainerScript(string fullName)
        {
            var parts = fullName.Split('.');
            if (parts.Length >= 2)
            {
                return parts[parts.Length - 2];
            }
            return string.Empty;
        }
        
        /// <summary>
        /// Determines if a test should be included based on filter and TestMode parameters
        /// </summary>
        private static bool ShouldIncludeTest(TestInfo testInfo, GetTestsParameters parameters)
        {
            // Apply filter if specified
            if (!string.IsNullOrEmpty(parameters.Filter))
            {
                if (testInfo.TestName.IndexOf(parameters.Filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    testInfo.TestNamespace.IndexOf(parameters.Filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }
            
            // Check TestMode filter
            bool isEditModeTest = testInfo.TestAssembly.Contains("Editor") || 
                                testInfo.TestNamespace.Contains("Editor") ||
                                testInfo.TestAssembly.Contains("Tests.Editor");
            bool isPlayModeTest = !isEditModeTest;
            
            if (parameters.TestMode == "All" ||
                (parameters.TestMode == "EditMode" && isEditModeTest) ||
                (parameters.TestMode == "PlayMode" && isPlayModeTest))
            {
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Main handler for the get_tests command
        /// </summary>
        /// <param name="params">JSON parameters containing TestMode and Filter</param>
        /// <returns>Response object with test information</returns>
        public static object HandleCommand(JObject @params)
        {
            try
            {
                // Parse parameters
                var parameters = new GetTestsParameters
                {
                    TestMode = @params?["TestMode"]?.ToString() ?? "All",
                    Filter = @params?["Filter"]?.ToString() ?? string.Empty
                };
                
                // Validate TestMode
                if (!IsValidTestMode(parameters.TestMode))
                {
                    return Response.Error($"Invalid TestMode: '{parameters.TestMode}'. Valid values are 'EditMode', 'PlayMode', or 'All'.");
                }
                
                // Try alternative approach using reflection to access Unity's test database directly
                var tests = new List<TestInfo>();
                
                try
                {
                    // Use reflection to access ALL assemblies and look for test methods more comprehensively
                    var allAssemblies = System.AppDomain.CurrentDomain.GetAssemblies().ToList();
                    var testAssemblies = new List<System.Reflection.Assembly>();
                    var assemblyTestCounts = new Dictionary<string, int>();
                    
                    foreach (var assembly in allAssemblies)
                    {
                        try
                        {
                            var assemblyName = assembly.GetName().Name;
                            
                            // Check if assembly contains any test methods
                            bool hasTests = false;
                            int testMethodCount = 0;
                            
                            var types = assembly.GetTypes();
                            foreach (var type in types)
                            {
                                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
                                    .Where(method => method.GetCustomAttributes(false)
                                        .Any(attr => 
                                            attr.GetType().Name == "TestAttribute" || 
                                            attr.GetType().Name == "TestCaseAttribute" ||
                                            attr.GetType().Name == "UnityTestAttribute" ||
                                            attr.GetType().Name == "TestCaseSourceAttribute" ||
                                            attr.GetType().Name == "ParameterizedTestAttribute" ||
                                            attr.GetType().Name == "TheoryAttribute" ||
                                            attr.GetType().Name.EndsWith("TestAttribute")))
                                    .ToList();
                                    
                                if (methods.Count > 0)
                                {
                                    hasTests = true;
                                    testMethodCount += methods.Count;
                                }
                            }
                            
                            if (hasTests)
                            {
                                testAssemblies.Add(assembly);
                                assemblyTestCounts[assemblyName] = testMethodCount;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            // Some assemblies might not be accessible, skip them
                            Debug.LogWarning($"[GetTests] Could not scan assembly {assembly.GetName().Name}: {ex.Message}");
                        }
                    }
                        
                    Debug.Log($"[GetTests] Found {testAssemblies.Count} assemblies with tests: {string.Join(", ", assemblyTestCounts.Select(kvp => $"{kvp.Key}({kvp.Value})"))}");
                    
                    foreach (var assembly in testAssemblies)
                    {
                        try
                        {
                            var types = assembly.GetTypes();
                            foreach (var type in types)
                            {
                                // Look for ALL possible test method attributes
                                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
                                    .Where(method => method.GetCustomAttributes(false)
                                        .Any(attr => 
                                            attr.GetType().Name == "TestAttribute" || 
                                            attr.GetType().Name == "TestCaseAttribute" ||
                                            attr.GetType().Name == "UnityTestAttribute" ||
                                            attr.GetType().Name == "TestCaseSourceAttribute" ||
                                            attr.GetType().Name == "ParameterizedTestAttribute" ||
                                            attr.GetType().Name == "TheoryAttribute" ||
                                            attr.GetType().Name.EndsWith("TestAttribute")))
                                    .ToList();
                                    
                                foreach (var method in methods)
                                {
                                    // Handle parameterized tests - count TestCase attributes separately
                                    var testCaseAttributes = method.GetCustomAttributes(false)
                                        .Where(attr => attr.GetType().Name == "TestCaseAttribute")
                                        .ToList();
                                        
                                    if (testCaseAttributes.Count > 0)
                                    {
                                        // For parameterized tests, create one entry per TestCase
                                        for (int i = 0; i < testCaseAttributes.Count; i++)
                                        {
                                            var testInfo = new TestInfo
                                            {
                                                TestName = $"{type.FullName}.{method.Name}({i})",
                                                TestAssembly = assembly.GetName().Name,
                                                TestNamespace = type.Namespace ?? "",
                                                ContainerScript = type.Name
                                            };
                                            
                                            if (ShouldIncludeTest(testInfo, parameters))
                                            {
                                                tests.Add(testInfo);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Regular test method
                                        var testInfo = new TestInfo
                                        {
                                            TestName = $"{type.FullName}.{method.Name}",
                                            TestAssembly = assembly.GetName().Name,
                                            TestNamespace = type.Namespace ?? "",
                                            ContainerScript = type.Name
                                        };
                                        
                                        if (ShouldIncludeTest(testInfo, parameters))
                                        {
                                            tests.Add(testInfo);
                                        }
                                    }
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[GetTests] Error processing assembly {assembly.GetName().Name}: {ex.Message}");
                        }
                    }
                    
                    Debug.Log($"[GetTests] Found {tests.Count} tests using reflection approach");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[GetTests] Reflection approach failed: {ex.Message}");
                    
                    // Fallback to original approach if reflection fails
                    return Response.Error($"Failed to retrieve tests using reflection: {ex.Message}");
                }
                
                // Convert to response format
                var testData = tests.Select(t => new
                {
                    TestName = t.TestName,
                    TestAssembly = t.TestAssembly,
                    TestNamespace = t.TestNamespace,
                    ContainerScript = t.ContainerScript
                }).ToList();
                
                return Response.Success($"Retrieved {tests.Count} tests.", testData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GetTests] Error retrieving tests: {e}");
                return Response.Error($"Failed to retrieve tests: {e.Message}");
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