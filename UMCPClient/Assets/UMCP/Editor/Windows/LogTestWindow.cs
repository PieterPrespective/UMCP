using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UMCP.Editor.Tools;
using UMCP.Editor.Helpers;

namespace UMCP.Editor.Windows
{
    /// <summary>
    /// Unity Editor window for testing log functionality with MarkStartOfNewStep and RequestStepLogs tools.
    /// Accessible via menu item 'UMCP/LogTestWindow'.
    /// </summary>
    public class LogTestWindow : EditorWindow
    {
        // UI state variables
        private string stepName = "TestStep";
        private Vector2 scrollPosition;
        private List<LogEntry> logEntries = new List<LogEntry>();
        private bool isProcessingLogs = false;
        private string lastOperationResult = "";
        private MessageType lastOperationMessageType = MessageType.Info;
        
        // UI styling
        private GUIStyle boldLabelStyle;
        private GUIStyle logEntryStyle;
        private GUIStyle logEntryBoxStyle;
        
        /// <summary>
        /// Simple data structure to hold log entry information for display
        /// </summary>
        [System.Serializable]
        public class LogEntry
        {
            public string type;
            public string message;
            public string file;
            public int line;
            public string stackTrace;
            public string timestamp;
            
            public LogEntry(string type, string message, string file = "", int line = 0, string stackTrace = "", string timestamp = "")
            {
                this.type = type;
                this.message = message;
                this.file = file;
                this.line = line;
                this.stackTrace = stackTrace;
                this.timestamp = timestamp;
            }
        }
        
        [MenuItem("UMCP/LogTestWindow")]
        public static void ShowWindow()
        {
            var window = GetWindow<LogTestWindow>("UMCP Log Test");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }
        
        private void OnEnable()
        {
            // Initialize UI styles
            InitializeStyles();
        }
        
        private void InitializeStyles()
        {
            boldLabelStyle = new GUIStyle(EditorStyles.boldLabel);
            
            logEntryStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
                richText = true
            };
            
            logEntryBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(4, 4, 2, 2)
            };
        }
        
        private void OnGUI()
        {
            if (boldLabelStyle == null)
                InitializeStyles();
                
            EditorGUILayout.Space(10);
            
            // Title
            EditorGUILayout.LabelField("UMCP Log Test Window", EditorStyles.largeLabel);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.HelpBox("This window allows you to test the MarkStartOfNewStep and RequestStepLogs tools.", MessageType.Info);
            EditorGUILayout.Space(10);
            
            // Step name input section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Step Configuration", boldLabelStyle);
            EditorGUILayout.Space(3);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Step Name:", GUILayout.Width(80));
            stepName = EditorGUILayout.TextField(stepName);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // Action buttons section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Actions", boldLabelStyle);
            EditorGUILayout.Space(3);
            
            EditorGUILayout.BeginHorizontal();
            
            // Mark Start of New Step button
            if (GUILayout.Button("Mark Start of New Step", GUILayout.Height(25)))
            {
                markStartOfNewStep();
            }
            
            // Request Step Logs button
            if (GUILayout.Button("Request Step Logs", GUILayout.Height(25)))
            {
                requestStepLogs();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Clear logs button
            EditorGUILayout.Space(5);
            if (GUILayout.Button("Clear Log Display", GUILayout.Height(20)))
            {
                ClearLogDisplay();
            }
            
            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();
            
            // Show last operation result if any
            if (!string.IsNullOrEmpty(lastOperationResult))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(lastOperationResult, lastOperationMessageType);
            }
            
            EditorGUILayout.Space(10);
            
            // Log display section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Log Entries", boldLabelStyle);
            EditorGUILayout.LabelField($"({logEntries.Count} entries)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3);
            
            if (isProcessingLogs)
            {
                EditorGUILayout.LabelField("Processing logs...", EditorStyles.centeredGreyMiniLabel);
            }
            else if (logEntries.Count == 0)
            {
                EditorGUILayout.LabelField("No logs to display. Use 'Request Step Logs' to load logs for a step.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                // Scrollable log display
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(200));
                
                foreach (var logEntry in logEntries)
                {
                    DrawLogEntry(logEntry);
                }
                
                EditorGUILayout.EndScrollView();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// Draws a single log entry with appropriate styling based on log type.
        /// </summary>
        private void DrawLogEntry(LogEntry logEntry)
        {
            Color originalColor = GUI.backgroundColor;
            
            // Set background color based on log type
            switch (logEntry.type.ToLower())
            {
                case "error":
                    GUI.backgroundColor = new Color(1f, 0.8f, 0.8f, 0.8f); // Light red
                    break;
                case "warning":
                    GUI.backgroundColor = new Color(1f, 1f, 0.8f, 0.8f); // Light yellow
                    break;
                case "log":
                default:
                    GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 0.8f); // Light gray
                    break;
            }
            
            EditorGUILayout.BeginVertical(logEntryBoxStyle);
            
            // Log type header
            EditorGUILayout.BeginHorizontal();
            string typeLabel = $"<b>[{logEntry.type.ToUpper()}]</b>";
            EditorGUILayout.LabelField(typeLabel, logEntryStyle, GUILayout.Width(60));
            
            // Note: Timestamp not currently available from Unity console logs
            
            EditorGUILayout.EndHorizontal();
            
            // Message content
            EditorGUILayout.LabelField(logEntry.message, logEntryStyle);
            
            // File and line info if available
            if (!string.IsNullOrEmpty(logEntry.file) && logEntry.line > 0)
            {
                EditorGUILayout.LabelField($"<i>at {logEntry.file}:{logEntry.line}</i>", logEntryStyle);
            }
            
            // Stack trace if available and not empty
            if (!string.IsNullOrEmpty(logEntry.stackTrace) && logEntry.stackTrace.Trim() != "")
            {
                if (GUILayout.Button("Show Stack Trace", EditorStyles.miniButton, GUILayout.Width(120)))
                {
                    // Toggle stack trace visibility by showing it in a popup window
                    ShowStackTraceWindow(logEntry.message, logEntry.stackTrace);
                }
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
            
            GUI.backgroundColor = originalColor;
        }
        
        /// <summary>
        /// Shows stack trace in a separate popup window.
        /// </summary>
        private void ShowStackTraceWindow(string logMessage, string stackTrace)
        {
            var window = CreateInstance<StackTraceWindow>();
            window.Initialize(logMessage, stackTrace);
            window.ShowUtility();
        }
        
        /// <summary>
        /// Invokes the MarkStartOfNewStep tool with the current step name.
        /// </summary>
        private void markStartOfNewStep()
        {
            if (string.IsNullOrEmpty(stepName.Trim()))
            {
                ShowOperationResult("Step name cannot be empty!", MessageType.Error);
                return;
            }
            
            try
            {
                // Create parameters for MarkStartOfNewStep tool
                var parameters = new JObject
                {
                    ["stepName"] = stepName.Trim()
                };
                
                // Invoke the tool
                var result = MarkStartOfNewStep.HandleCommand(parameters);
                
                // Process the result
                dynamic response = result;
                if (response.success == true)
                {
                    ShowOperationResult($"Successfully marked start of step '{stepName}'", MessageType.Info);
                    Debug.Log($"[LogTestWindow] Marked start of step: {stepName}");
                }
                else
                {
                    string errorMessage = response.error?.ToString() ?? "Unknown error occurred";
                    ShowOperationResult($"Failed to mark step start: {errorMessage}", MessageType.Error);
                }
            }
            catch (System.Exception e)
            {
                ShowOperationResult($"Error invoking MarkStartOfNewStep: {e.Message}", MessageType.Error);
                Debug.LogError($"[LogTestWindow] Error in MarkStartOfNewStep: {e.Message}");
            }
        }
        
        /// <summary>
        /// Invokes the RequestStepLogs tool and displays the results.
        /// </summary>
        private void requestStepLogs()
        {
            if (string.IsNullOrEmpty(stepName.Trim()))
            {
                ShowOperationResult("Step name cannot be empty!", MessageType.Error);
                return;
            }
            
            isProcessingLogs = true;
            
            try
            {
                // Create parameters for RequestStepLogs tool
                var parameters = new JObject
                {
                    ["stepName"] = stepName.Trim(),
                    ["includeStacktrace"] = true,
                    ["format"] = "detailed"
                };
                
                // Invoke the tool
                var result = RequestStepLogs.HandleCommand(parameters);
                
                // Process the result
                dynamic response = result;
                if (response.success == true)
                {
                    // Clear existing log entries
                    logEntries.Clear();
                    
                    // Process the log data
                    if (response.data != null)
                    {
                        var logData = response.data as System.Collections.IEnumerable;
                        if (logData != null)
                        {
                            foreach (dynamic logItem in logData)
                            {
                                try
                                {
                                    var logEntry = new LogEntry(
                                        type: logItem.type?.ToString() ?? "log",
                                        message: logItem.message?.ToString() ?? "",
                                        file: logItem.file?.ToString() ?? "",
                                        line: logItem.line != null ? (int)logItem.line : 0,
                                        stackTrace: logItem.stackTrace?.ToString() ?? "",
                                        timestamp: "" // Timestamp not available in current data structure
                                    );
                                    
                                    logEntries.Add(logEntry);
                                }
                                catch (System.Exception ex)
                                {
                                    Debug.LogError($"[LogTestWindow] Error processing log item: {ex.Message}");
                                    Debug.LogError($"[LogTestWindow] Log item type: {logItem?.GetType()}");
                                    
                                    // Add a fallback entry to show the error
                                    var errorEntry = new LogEntry(
                                        type: "error",
                                        message: $"Error processing log entry: {ex.Message}",
                                        file: "",
                                        line: 0,
                                        stackTrace: "",
                                        timestamp: ""
                                    );
                                    logEntries.Add(errorEntry);
                                }
                            }
                        }
                    }
                    
                    string message = response.message?.ToString() ?? $"Retrieved {logEntries.Count} log entries";
                    ShowOperationResult(message, MessageType.Info);
                    Debug.Log($"[LogTestWindow] Retrieved {logEntries.Count} log entries for step: {stepName}");
                }
                else
                {
                    string errorMessage = response.error?.ToString() ?? "Unknown error occurred";
                    ShowOperationResult($"Failed to retrieve step logs: {errorMessage}", MessageType.Warning);
                    logEntries.Clear();
                }
            }
            catch (System.Exception e)
            {
                ShowOperationResult($"Error invoking RequestStepLogs: {e.Message}", MessageType.Error);
                Debug.LogError($"[LogTestWindow] Error in RequestStepLogs: {e.Message}");
                logEntries.Clear();
            }
            finally
            {
                isProcessingLogs = false;
            }
        }
        
        /// <summary>
        /// Clears the log display.
        /// </summary>
        private void ClearLogDisplay()
        {
            logEntries.Clear();
            lastOperationResult = "";
            Debug.Log("[LogTestWindow] Log display cleared");
        }
        
        /// <summary>
        /// Shows the result of the last operation in the UI.
        /// </summary>
        private void ShowOperationResult(string message, MessageType messageType)
        {
            lastOperationResult = message;
            lastOperationMessageType = messageType;
        }
    }
    
    /// <summary>
    /// Simple popup window to display stack traces.
    /// </summary>
    public class StackTraceWindow : EditorWindow
    {
        private string logMessage;
        private string stackTrace;
        private Vector2 scrollPosition;
        
        public void Initialize(string logMessage, string stackTrace)
        {
            this.logMessage = logMessage;
            this.stackTrace = stackTrace;
            this.titleContent = new GUIContent("Stack Trace");
            this.minSize = new Vector2(500, 300);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Log Message:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(logMessage, EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Stack Trace:", EditorStyles.boldLabel);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.SelectableLabel(stackTrace, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(5);
            if (GUILayout.Button("Close"))
            {
                Close();
            }
        }
    }
}
