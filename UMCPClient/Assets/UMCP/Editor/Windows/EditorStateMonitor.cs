using UnityEngine;
using UnityEditor;
using UMCP.Editor.Helpers;

namespace UMCP.Editor.Windows
{
    /// <summary>
    /// Editor window for monitoring Unity Editor states
    /// </summary>
    public class EditorStateMonitor : EditorWindow
    {
        private GUIStyle headerStyle;
        private GUIStyle stateStyle;
        private GUIStyle contextStyle;
        private Vector2 scrollPosition;
        private string stateLog = "";
        private bool autoScroll = true;

        [MenuItem("UMCP/Editor State Monitor")]
        public static void ShowWindow()
        {
            var window = GetWindow<EditorStateMonitor>("Editor State Monitor");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            // Subscribe to state changes
            EditorStateHelper.OnRunmodeChanged += OnRunmodeChanged;
            EditorStateHelper.OnContextChanged += OnContextChanged;
            EditorStateHelper.OnStateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            // Unsubscribe from state changes
            EditorStateHelper.OnRunmodeChanged -= OnRunmodeChanged;
            EditorStateHelper.OnContextChanged -= OnContextChanged;
            EditorStateHelper.OnStateChanged -= OnStateChanged;
        }

        private void OnGUI()
        {
            InitializeStyles();

            EditorGUILayout.Space(10);

            // Header
            EditorGUILayout.LabelField("Unity Editor State Monitor", headerStyle);
            EditorGUILayout.Space(5);

            // Current State Display
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.Space(5);
                
                // Runmode
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Runmode:", EditorStyles.boldLabel, GUILayout.Width(100));
                var runmodeColor = GetRunmodeColor(EditorStateHelper.CurrentRunmode);
                GUI.color = runmodeColor;
                EditorGUILayout.LabelField(EditorStateHelper.CurrentRunmode.ToString(), stateStyle);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();

                // Context
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Context:", EditorStyles.boldLabel, GUILayout.Width(100));
                var contextColor = GetContextColor(EditorStateHelper.CurrentContext);
                GUI.color = contextColor;
                EditorGUILayout.LabelField(EditorStateHelper.CurrentContext.ToString(), contextStyle);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // Status Indicators
                EditorGUILayout.BeginHorizontal();
                DrawStatusIndicator("Can Modify Files", EditorStateHelper.CanModifyProjectFiles);
                DrawStatusIndicator("Editor Responsive", EditorStateHelper.IsEditorResponsive);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // State Log
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("State Change Log", EditorStyles.boldLabel);
                autoScroll = EditorGUILayout.Toggle("Auto Scroll", autoScroll, GUILayout.Width(100));
                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    stateLog = "";
                }
                EditorGUILayout.EndHorizontal();

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, EditorStyles.helpBox, GUILayout.Height(150));
                EditorGUILayout.TextArea(stateLog, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                if (autoScroll && Event.current.type == EventType.Repaint)
                {
                    scrollPosition.y = float.MaxValue;
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Actions
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Test Actions", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh State"))
                {
                    EditorStateHelper.RefreshState();
                }
                if (GUILayout.Button("Force Recompile"))
                {
                    AssetDatabase.Refresh();
                    UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            // Force repaint during state changes
            if (EditorStateHelper.CurrentContext == EditorStateHelper.Context.Switching ||
                EditorStateHelper.CurrentContext == EditorStateHelper.Context.Compiling ||
                EditorStateHelper.CurrentContext == EditorStateHelper.Context.UpdatingAssets)
            {
                Repaint();
            }
        }

        private void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (stateStyle == null)
            {
                stateStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 12
                };
            }

            if (contextStyle == null)
            {
                contextStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 12
                };
            }
        }

        private Color GetRunmodeColor(EditorStateHelper.Runmode runmode)
        {
            return runmode switch
            {
                EditorStateHelper.Runmode.EditMode_Scene => Color.green,
                EditorStateHelper.Runmode.EditMode_Prefab => Color.cyan,
                EditorStateHelper.Runmode.PlayMode => Color.yellow,
                _ => Color.white
            };
        }

        private Color GetContextColor(EditorStateHelper.Context context)
        {
            return context switch
            {
                EditorStateHelper.Context.Running => Color.green,
                EditorStateHelper.Context.Switching => Color.yellow,
                EditorStateHelper.Context.Compiling => new Color(1f, 0.5f, 0f), // Orange
                EditorStateHelper.Context.UpdatingAssets => Color.magenta,
                _ => Color.white
            };
        }

        private void DrawStatusIndicator(string label, bool status)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label + ":", GUILayout.Width(120));
            GUI.color = status ? Color.green : Color.red;
            EditorGUILayout.LabelField(status ? "✓" : "✗", GUILayout.Width(20));
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void OnRunmodeChanged(EditorStateHelper.Runmode previous, EditorStateHelper.Runmode current)
        {
            LogStateChange($"Runmode changed: {previous} → {current}");
            Repaint();
        }

        private void OnContextChanged(EditorStateHelper.Context previous, EditorStateHelper.Context current)
        {
            LogStateChange($"Context changed: {previous} → {current}");
            Repaint();
        }

        private void OnStateChanged()
        {
            Repaint();
        }

        private void LogStateChange(string message)
        {
            var timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
            stateLog += $"[{timestamp}] {message}\n";
            
            // Limit log size
            var lines = stateLog.Split('\n');
            if (lines.Length > 100)
            {
                stateLog = string.Join("\n", lines, lines.Length - 100, 100);
            }
        }
    }
}