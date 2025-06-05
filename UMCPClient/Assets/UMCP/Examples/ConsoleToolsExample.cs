using UnityEngine;
using UnityEditor;
using UMCP.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace UMCP.Examples
{
    /// <summary>
    /// Example script demonstrating the usage of UMCP Console Tools.
    /// This shows how to use the step marking and log retrieval features.
    /// </summary>
    public class ConsoleToolsExample : EditorWindow
    {
        private string currentStepName = "MyDevelopmentStep";
        private string stepToRetrieve = "MyDevelopmentStep";
        private Vector2 scrollPosition;
        private string logOutput = "";
        
        [MenuItem("UMCP/Examples/Console Tools Example")]
        public static void ShowWindow()
        {
            GetWindow<ConsoleToolsExample>("UMCP Console Tools Example");
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("UMCP Console Tools Example", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Section 1: Mark Start of Step
            EditorGUILayout.LabelField("1. Mark Start of Development Step", EditorStyles.boldLabel);
            currentStepName = EditorGUILayout.TextField("Step Name:", currentStepName);
            
            if (GUILayout.Button("Mark Start of Step"))
            {
                MarkNewStep(currentStepName);
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "After marking a step, any logs that appear in the Unity Console will be associated with this step.",
                MessageType.Info
            );
            
            EditorGUILayout.Space();
            
            // Section 2: Generate Test Logs
            EditorGUILayout.LabelField("2. Generate Test Logs", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Log Message"))
            {
                Debug.Log($"Test log message for step: {currentStepName}");
            }
            
            if (GUILayout.Button("Log Warning"))
            {
                Debug.LogWarning($"Test warning for step: {currentStepName}");
            }
            
            if (GUILayout.Button("Log Error"))
            {
                Debug.LogError($"Test error for step: {currentStepName}");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Section 3: Retrieve Step Logs
            EditorGUILayout.LabelField("3. Retrieve Logs for Step", EditorStyles.boldLabel);
            stepToRetrieve = EditorGUILayout.TextField("Step Name to Retrieve:", stepToRetrieve);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Get Step Logs (Detailed)"))
            {
                RetrieveStepLogs(stepToRetrieve, "detailed");
            }
            
            if (GUILayout.Button("Get Step Logs (Plain)"))
            {
                RetrieveStepLogs(stepToRetrieve, "plain");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Section 4: Console Operations
            EditorGUILayout.LabelField("4. Console Operations", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Read All Logs"))
            {
                ReadAllLogs();
            }
            
            if (GUILayout.Button("Read Errors Only"))
            {
                ReadErrorLogs();
            }
            
            if (GUILayout.Button("Clear Console"))
            {
                ClearConsole();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Output Section
            EditorGUILayout.LabelField("Output:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            EditorGUILayout.TextArea(logOutput, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            
            if (GUILayout.Button("Clear Output"))
            {
                logOutput = "";
            }
        }
        
        private void MarkNewStep(string stepName)
        {
            var result = MarkStartOfNewStep.HandleCommand(new JObject { ["stepName"] = stepName });
            HandleResult("MarkStartOfNewStep", result);
        }
        
        private void RetrieveStepLogs(string stepName, string format)
        {
            var result = RequestStepLogs.HandleCommand(new JObject 
            { 
                ["stepName"] = stepName,
                ["format"] = format,
                ["includeStacktrace"] = false // Keep output cleaner for example
            });
            HandleResult($"RequestStepLogs ({format})", result);
        }
        
        private void ReadAllLogs()
        {
            var result = ReadConsole.HandleCommand(new JObject 
            { 
                ["action"] = "get",
                ["count"] = 20,
                ["format"] = "detailed",
                ["includeStacktrace"] = false
            });
            HandleResult("ReadConsole (All)", result);
        }
        
        private void ReadErrorLogs()
        {
            var result = ReadConsole.HandleCommand(new JObject 
            { 
                ["action"] = "get",
                ["types"] = new JArray("error"),
                ["count"] = 10,
                ["format"] = "detailed",
                ["includeStacktrace"] = true
            });
            HandleResult("ReadConsole (Errors)", result);
        }
        
        private void ClearConsole()
        {
            var result = ReadConsole.HandleCommand(new JObject { ["action"] = "clear" });
            HandleResult("ClearConsole", result);
        }
        
        private void HandleResult(string operation, object result)
        {
            logOutput += $"\n=== {operation} ===\n";
            
            if (result is JObject jObj)
            {
                logOutput += jObj.ToString(Newtonsoft.Json.Formatting.Indented) + "\n";
            }
            else
            {
                logOutput += result.ToString() + "\n";
            }
            
            // Ensure the GUI updates
            Repaint();
        }
    }
    
    /// <summary>
    /// Example MonoBehaviour that demonstrates automated step tracking.
    /// </summary>
    public class StepTrackingExample : MonoBehaviour
    {
        private void Start()
        {
            // Mark the start of initialization
            MarkStartOfNewStep.HandleCommand(new JObject { ["stepName"] = "GameObjectInitialization" });
            
            Debug.Log("Starting GameObject initialization...");
            
            // Simulate some initialization steps
            InitializeComponents();
            LoadResources();
            SetupReferences();
            
            Debug.Log("GameObject initialization complete!");
            
            // Later, you can retrieve all logs for this initialization
            // var logs = RequestStepLogs.HandleCommand(new JObject { ["stepName"] = "GameObjectInitialization" });
        }
        
        private void InitializeComponents()
        {
            Debug.Log("Initializing components...");
            // Component initialization logic
        }
        
        private void LoadResources()
        {
            Debug.Log("Loading resources...");
            // Resource loading logic
        }
        
        private void SetupReferences()
        {
            Debug.Log("Setting up references...");
            // Reference setup logic
        }
    }
}
