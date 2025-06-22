using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using Unity.EditorCoroutines.Editor;
using UMCP.Editor.Tools;
using UMCP.Editor.Helpers;

namespace UMCP.Editor.Windows
{
    /// <summary>
    /// Unity Editor window for testing GetTests and RunTests tools
    /// Provides UI controls for all tool parameters and displays results
    /// </summary>
    public class TestToolsWindow : EditorWindow
    {
        #region Fields
        
        private Vector2 scrollPosition;
        private bool showGetTestsSection = true;
        private bool showRunTestsSection = true;
        
        // GetTests parameters
        private int getTestsModeIndex = 0;
        private string getTestsFilter = "";
        private readonly string[] testModeOptions = { "All", "EditMode", "PlayMode" };
        
        // RunTests parameters
        private int runTestsModeIndex = 0;
        private List<string> selectedTests = new List<string>();
        private bool outputTestResults = true;
        private bool outputLogData = true;
        private string manualTestFilter = "";
        
        // Results
        private string getTestsResult = "";
        private string runTestsResult = "";
        private List<TestInfo> availableTests = new List<TestInfo>();
        private bool[] testSelections = new bool[0];
        
        // UI State
        private bool isExecutingGetTests = false;
        private bool isExecutingRunTests = false;
        private Vector2 getTestsScrollPos;
        private Vector2 runTestsScrollPos;
        private Vector2 resultScrollPos;
        private EditorCoroutine runTestsTimeoutCoroutine;
        
        #endregion
        
        #region Unity Methods
        
        [MenuItem("UMCP/Test Tools Window")]
        public static void ShowWindow()
        {
            GetWindow<TestToolsWindow>("UMCP Test Tools");
        }
        
        private void OnEnable()
        {
            // Initialize with default values
            RefreshAvailableTests();
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            EditorGUILayout.LabelField("UMCP Test Tools", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            DrawGetTestsSection();
            EditorGUILayout.Space(10);
            DrawRunTestsSection();
            EditorGUILayout.Space(10);
            DrawResultsSection();
            
            EditorGUILayout.EndScrollView();
        }
        
        #endregion
        
        #region GetTests Section
        
        private void DrawGetTestsSection()
        {
            showGetTestsSection = EditorGUILayout.Foldout(showGetTestsSection, "GetTests Tool", true, EditorStyles.foldoutHeader);
            
            if (!showGetTestsSection) return;
            
            EditorGUI.indentLevel++;
            
            // TestMode parameter
            EditorGUILayout.LabelField("Test Mode", EditorStyles.boldLabel);
            getTestsModeIndex = EditorGUILayout.Popup("Mode:", getTestsModeIndex, testModeOptions);
            
            EditorGUILayout.Space(5);
            
            // Filter parameter
            EditorGUILayout.LabelField("Filter", EditorStyles.boldLabel);
            getTestsFilter = EditorGUILayout.TextField("Filter Text:", getTestsFilter);
            EditorGUILayout.HelpBox("Leave empty to get all tests. Enter text to filter by test name or namespace.", MessageType.Info);
            
            EditorGUILayout.Space(5);
            
            // Execute button
            EditorGUI.BeginDisabledGroup(isExecutingGetTests);
            if (GUILayout.Button("Execute GetTests", GUILayout.Height(30)))
            {
                ExecuteGetTests();
            }
            EditorGUI.EndDisabledGroup();
            
            if (isExecutingGetTests)
            {
                EditorGUILayout.HelpBox("Executing GetTests...", MessageType.Info);
            }
            
            EditorGUI.indentLevel--;
        }
        
        private void ExecuteGetTests()
        {
            isExecutingGetTests = true;
            getTestsResult = "Retrieving tests...";
            
            // Use async method to avoid blocking Unity's main thread
            var parameters = new GetTestsParameters
            {
                TestMode = testModeOptions[getTestsModeIndex],
                Filter = getTestsFilter ?? string.Empty
            };
            
            GetTestsUtility.GetTestsByParametersAsync(parameters, (tests) =>
            {
                
                // Update UI on main thread
                availableTests.Clear();
                availableTests.AddRange(tests);
                
                // Update test selections array
                testSelections = new bool[availableTests.Count];
                
                getTestsResult = $"Success: Found {availableTests.Count} tests\n\n";
                if (availableTests.Count > 0)
                {
                    getTestsResult += string.Join("\n", availableTests.Select(t => 
                        $"• {t.TestName} (Assembly: {t.TestAssembly}, Namespace: {t.TestNamespace})"));
                }
                else
                {
                    getTestsResult += "No tests found matching the criteria.";
                }
                
                isExecutingGetTests = false;
                
                // Force UI repaint
                Repaint();
            });
        }
        
        #endregion
        
        #region RunTests Section
        
        private void DrawRunTestsSection()
        {
            showRunTestsSection = EditorGUILayout.Foldout(showRunTestsSection, "RunTests Tool", true, EditorStyles.foldoutHeader);
            
            if (!showRunTestsSection) return;
            
            EditorGUI.indentLevel++;
            
            // TestMode parameter
            EditorGUILayout.LabelField("Test Mode", EditorStyles.boldLabel);
            runTestsModeIndex = EditorGUILayout.Popup("Mode:", runTestsModeIndex, testModeOptions);
            
            EditorGUILayout.Space(5);
            
            // Test selection
            EditorGUILayout.LabelField("Test Selection", EditorStyles.boldLabel);
            
            if (availableTests.Count > 0)
            {
                EditorGUILayout.LabelField($"Available Tests ({availableTests.Count}):");
                
                runTestsScrollPos = EditorGUILayout.BeginScrollView(runTestsScrollPos, GUILayout.Height(150));
                
                // Select All / Deselect All buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All", GUILayout.Width(80)))
                {
                    for (int i = 0; i < testSelections.Length; i++)
                        testSelections[i] = true;
                }
                if (GUILayout.Button("Deselect All", GUILayout.Width(80)))
                {
                    for (int i = 0; i < testSelections.Length; i++)
                        testSelections[i] = false;
                }
                EditorGUILayout.EndHorizontal();
                
                // Individual test checkboxes
                for (int i = 0; i < availableTests.Count && i < testSelections.Length; i++)
                {
                    testSelections[i] = EditorGUILayout.ToggleLeft(availableTests[i].TestName, testSelections[i]);
                }
                
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("No tests available. Run GetTests first to populate the test list.", MessageType.Warning);
                
                // Manual test filter for direct entry
                EditorGUILayout.LabelField("Manual Test Entry", EditorStyles.boldLabel);
                manualTestFilter = EditorGUILayout.TextField("Test Names (comma-separated):", manualTestFilter);
                EditorGUILayout.HelpBox("Enter test names directly (e.g., 'MyNamespace.MyClass.TestMethod1, MyNamespace.MyClass.TestMethod2')", MessageType.Info);
            }
            
            EditorGUILayout.Space(5);
            
            // Output options
            EditorGUILayout.LabelField("Output Options", EditorStyles.boldLabel);
            outputTestResults = EditorGUILayout.Toggle("Output Test Results:", outputTestResults);
            outputLogData = EditorGUILayout.Toggle("Output Log Data:", outputLogData);
            
            EditorGUILayout.Space(5);
            
            // Check if Unity is in correct state
            bool canRunTests = EditorStateHelper.CanModifyProjectFiles;
            if (!canRunTests)
            {
                EditorGUILayout.HelpBox($"Cannot run tests in current Unity state: {EditorStateHelper.CurrentContext}", MessageType.Warning);
            }
            
            // Execute button
            EditorGUI.BeginDisabledGroup(isExecutingRunTests || !canRunTests);
            if (GUILayout.Button("Execute RunTests", GUILayout.Height(30)))
            {
                ExecuteRunTests();
            }
            EditorGUI.EndDisabledGroup();
            
            if (isExecutingRunTests)
            {
                EditorGUILayout.HelpBox("Executing RunTests... This may take some time.", MessageType.Info);
            }
            
            EditorGUI.indentLevel--;
        }
        
        private void ExecuteRunTests()
        {
            isExecutingRunTests = true;
            runTestsResult = "Starting test execution...";
            
            try
            {
                // Build parameters
                var parameters = new RunTestsParameters
                {
                    TestMode = testModeOptions[runTestsModeIndex],
                    OutputTestResults = outputTestResults,
                    OutputLogData = outputLogData
                };
                
                // Determine which tests to run
                List<string> testsToRun = new List<string>();
                
                if (availableTests.Count > 0 && testSelections.Length > 0)
                {
                    // Use selected tests from the list
                    for (int i = 0; i < testSelections.Length && i < availableTests.Count; i++)
                    {
                        if (testSelections[i])
                        {
                            testsToRun.Add(availableTests[i].TestName);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(manualTestFilter))
                {
                    // Use manually entered test names
                    testsToRun = manualTestFilter.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();
                }
                
                parameters.Filter = testsToRun.ToArray();
                
                // Force immediate UI update
                Repaint();
                
                // Start timeout coroutine to ensure UI state is reset
                if (runTestsTimeoutCoroutine != null)
                {
                    EditorCoroutineUtility.StopCoroutine(runTestsTimeoutCoroutine);
                }
                runTestsTimeoutCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(RunTestsTimeout());
                
                // Use async method to avoid blocking Unity's main thread
                RunTestsUtility.RunTestsByParameters(parameters, (result) =>
                {
                    // Stop timeout coroutine since we got a response
                    if (runTestsTimeoutCoroutine != null)
                    {
                        EditorCoroutineUtility.StopCoroutine(runTestsTimeoutCoroutine);
                        runTestsTimeoutCoroutine = null;
                    }
                    
                    // Check if result contains error message (from already running tests)
                    if (result.TestResults == null && !string.IsNullOrEmpty(result.LogData) && result.LogData.Contains("already running"))
                    {
                        runTestsResult = "Error: " + result.LogData;
                    }
                    else
                    {
                        // Update UI on main thread
                        runTestsResult = "Test execution completed!\n\n";
                        runTestsResult += $"All Tests Successful: {result.AllSuccess}\n\n";
                        
                        if (outputTestResults && result.TestResults != null)
                        {
                            runTestsResult += "Test Results:\n";
                            foreach (var testResult in result.TestResults)
                            {
                                string status = testResult.Success ? "✓ PASS" : "✗ FAIL";
                                runTestsResult += $"{status}: {testResult.TestName} ({testResult.Duration:F3}s)\n";
                                
                                // Show failure details if test failed
                                if (!testResult.Success && !string.IsNullOrEmpty(testResult.FailureMessage))
                                {
                                    runTestsResult += $"   Error: {testResult.FailureMessage}\n";
                                    if (!string.IsNullOrEmpty(testResult.StackTrace))
                                    {
                                        // Show first 3 lines of stack trace for brevity
                                        var stackLines = testResult.StackTrace.Split('\n');
                                        var linesToShow = System.Math.Min(3, stackLines.Length);
                                        for (int i = 0; i < linesToShow; i++)
                                        {
                                            runTestsResult += $"   {stackLines[i].Trim()}\n";
                                        }
                                        if (stackLines.Length > 3)
                                        {
                                            runTestsResult += "   ...\n";
                                        }
                                    }
                                    runTestsResult += "\n";
                                }
                            }
                            runTestsResult += "\n";
                        }
                        
                        if (outputLogData && !string.IsNullOrEmpty(result.LogData))
                        {
                            runTestsResult += "Log Data:\n";
                            runTestsResult += result.LogData;
                        }
                    }
                    
                    isExecutingRunTests = false;
                    
                    // Force UI repaint
                    Repaint();
                });
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TestToolsWindow] ExecuteRunTests exception: {ex}");
                runTestsResult = $"Error: {ex.Message}";
                isExecutingRunTests = false;
                Repaint();
            }
        }
        
        #endregion
        
        #region Results Section
        
        private void DrawResultsSection()
        {
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
            
            resultScrollPos = EditorGUILayout.BeginScrollView(resultScrollPos, GUILayout.Height(200));
            
            // GetTests Results
            if (!string.IsNullOrEmpty(getTestsResult))
            {
                EditorGUILayout.LabelField("GetTests Result:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(getTestsResult, GUILayout.ExpandHeight(true));
                EditorGUILayout.Space(10);
            }
            
            // RunTests Results
            if (!string.IsNullOrEmpty(runTestsResult))
            {
                EditorGUILayout.LabelField("RunTests Result:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(runTestsResult, GUILayout.ExpandHeight(true));
            }
            
            EditorGUILayout.EndScrollView();
            
            // Clear results button
            if (GUILayout.Button("Clear Results"))
            {
                getTestsResult = "";
                runTestsResult = "";
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private IEnumerator RunTestsTimeout()
        {
            // Wait for 10 minutes (600 seconds)
            yield return new EditorWaitForSeconds(600f);
            
            // If still executing after timeout, reset state
            if (isExecutingRunTests)
            {
                runTestsResult = "Test execution timed out after 10 minutes.";
                isExecutingRunTests = false;
                Repaint();
            }
        }
        
        private void RefreshAvailableTests()
        {
            // Automatically run GetTests with "All" mode on window open
            if (availableTests.Count == 0)
            {
                var parameters = new GetTestsParameters
                {
                    TestMode = "All",
                    Filter = string.Empty
                };
                
                GetTestsUtility.GetTestsByParametersAsync(parameters, (tests) =>
                {
                    
                    availableTests.Clear();
                    availableTests.AddRange(tests);
                    testSelections = new bool[availableTests.Count];
                    
                    // Force UI repaint
                    Repaint();
                });
            }
        }
        
        #endregion
    }
}