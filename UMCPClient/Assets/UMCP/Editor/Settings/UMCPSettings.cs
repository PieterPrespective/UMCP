using UnityEngine;
using UnityEditor;

namespace UMCP.Editor.Settings
{
    /// <summary>
    /// Settings for UMCP Unity Client
    /// </summary>
    [System.Serializable]
    public class UMCPSettings : ScriptableObject
    {
        private static UMCPSettings _instance;
        
        [Header("Network Configuration")]
        [SerializeField]
        [Tooltip("Port for receiving MCP commands")]
        private int commandPort = 6400;
        
        [SerializeField]
        [Tooltip("Port for sending state updates")]
        private int statePort = 6401;
        
        [SerializeField]
        [Tooltip("IP address to bind listeners to")]
        private string bindAddress = "127.0.0.1";
        
        [Header("Connection Settings")]
        [SerializeField]
        [Tooltip("Socket timeout in seconds")]
        private int socketTimeout = 60;
        
        [SerializeField]
        [Tooltip("Send timeout for state updates in seconds")]
        private int stateSendTimeout = 5;
        
        public int CommandPort => commandPort;
        public int StatePort => statePort;
        public string BindAddress => bindAddress;
        public int SocketTimeout => socketTimeout;
        public int StateSendTimeout => stateSendTimeout;
        
        /// <summary>
        /// Get or create the settings instance
        /// </summary>
        public static UMCPSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Try to load existing settings
                    _instance = AssetDatabase.LoadAssetAtPath<UMCPSettings>("Assets/UMCP/Editor/Settings/UMCPSettings.asset");
                    
                    if (_instance == null)
                    {
                        // Create new settings asset
                        _instance = CreateInstance<UMCPSettings>();
                        
                        // Ensure directory exists
                        if (!AssetDatabase.IsValidFolder("Assets/UMCP"))
                            AssetDatabase.CreateFolder("Assets", "UMCP");
                        if (!AssetDatabase.IsValidFolder("Assets/UMCP/Editor"))
                            AssetDatabase.CreateFolder("Assets/UMCP", "Editor");
                        if (!AssetDatabase.IsValidFolder("Assets/UMCP/Editor/Settings"))
                            AssetDatabase.CreateFolder("Assets/UMCP/Editor", "Settings");
                        
                        AssetDatabase.CreateAsset(_instance, "Assets/UMCP/Editor/Settings/UMCPSettings.asset");
                        AssetDatabase.SaveAssets();
                    }
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// Open settings in inspector
        /// </summary>
        [MenuItem("UMCP/Settings")]
        public static void OpenSettings()
        {
            Selection.activeObject = Instance;
            EditorGUIUtility.PingObject(Instance);
        }
        
        /// <summary>
        /// Validate port numbers
        /// </summary>
        private void OnValidate()
        {
            commandPort = Mathf.Clamp(commandPort, 1024, 65535);
            statePort = Mathf.Clamp(statePort, 1024, 65535);
            
            if (commandPort == statePort)
            {
                Debug.LogWarning("Command port and state port should be different!");
            }
            
            socketTimeout = Mathf.Max(1, socketTimeout);
            stateSendTimeout = Mathf.Max(1, stateSendTimeout);
        }
    }
}
