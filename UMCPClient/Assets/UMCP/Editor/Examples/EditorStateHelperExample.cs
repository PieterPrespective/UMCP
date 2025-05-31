using UnityEngine;
using UnityEditor;
using UMCP.Editor.Helpers;

namespace UMCP.Editor.Examples
{
    /// <summary>
    /// Example class demonstrating how to use EditorStateHelper
    /// </summary>
    [InitializeOnLoad]
    public static class EditorStateHelperExample
    {
        static EditorStateHelperExample()
        {
            // Subscribe to state changes
            EditorStateHelper.OnRunmodeChanged += HandleRunmodeChange;
            EditorStateHelper.OnContextChanged += HandleContextChange;
        }

        /// <summary>
        /// Example method that checks if it's safe to modify project files
        /// </summary>
        public static bool TryModifyAsset(string assetPath)
        {
            if (!EditorStateHelper.CanModifyProjectFiles)
            {
                Debug.LogWarning($"Cannot modify asset '{assetPath}' - Editor is in {EditorStateHelper.CurrentRunmode} mode with {EditorStateHelper.CurrentContext} context");
                return false;
            }

            // Proceed with modification
            Debug.Log($"Modifying asset '{assetPath}' - Editor state allows modifications");
            return true;
        }

        /// <summary>
        /// Example method that waits for the editor to be responsive
        /// </summary>
        public static void PerformOperationWhenReady(System.Action operation)
        {
            if (EditorStateHelper.IsEditorResponsive)
            {
                operation?.Invoke();
            }
            else
            {
                // Wait for editor to become responsive
                EditorApplication.delayCall += () => PerformOperationWhenReady(operation);
            }
        }

        /// <summary>
        /// Example method that defers operations during specific contexts
        /// </summary>
        public static void SafeAssetDatabaseRefresh()
        {
            switch (EditorStateHelper.CurrentContext)
            {
                case EditorStateHelper.Context.Compiling:
                    Debug.Log("Deferring AssetDatabase.Refresh() - currently compiling");
                    EditorApplication.delayCall += SafeAssetDatabaseRefresh;
                    break;
                    
                case EditorStateHelper.Context.UpdatingAssets:
                    Debug.Log("Deferring AssetDatabase.Refresh() - currently updating assets");
                    EditorApplication.delayCall += SafeAssetDatabaseRefresh;
                    break;
                    
                case EditorStateHelper.Context.Switching:
                    Debug.Log("Deferring AssetDatabase.Refresh() - currently switching states");
                    EditorApplication.delayCall += SafeAssetDatabaseRefresh;
                    break;
                    
                case EditorStateHelper.Context.Running:
                    if (EditorStateHelper.CurrentRunmode != EditorStateHelper.Runmode.PlayMode)
                    {
                        Debug.Log("Performing AssetDatabase.Refresh()");
                        AssetDatabase.Refresh();
                    }
                    else
                    {
                        Debug.LogWarning("Cannot refresh AssetDatabase during PlayMode");
                    }
                    break;
            }
        }

        private static void HandleRunmodeChange(EditorStateHelper.Runmode previous, EditorStateHelper.Runmode current)
        {
            Debug.Log($"[EditorStateHelper] Runmode changed from {previous} to {current}");

            // Example: Save scene when exiting play mode
            if (previous == EditorStateHelper.Runmode.PlayMode && current == EditorStateHelper.Runmode.EditMode_Scene)
            {
                Debug.Log("Exited PlayMode - consider saving the scene");
                // EditorSceneManager.SaveOpenScenes();
            }

            // Example: Disable certain features in play mode
            if (current == EditorStateHelper.Runmode.PlayMode)
            {
                Debug.Log("Entered PlayMode - disabling asset modifications");
            }
        }

        private static void HandleContextChange(EditorStateHelper.Context previous, EditorStateHelper.Context current)
        {
            Debug.Log($"[EditorStateHelper] Context changed from {previous} to {current}");

            // Example: Show progress bar during compilation
            if (current == EditorStateHelper.Context.Compiling)
            {
                EditorUtility.DisplayProgressBar("Compiling", "Please wait...", 0.5f);
            }
            else if (previous == EditorStateHelper.Context.Compiling)
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Example MenuItem that respects editor state
        /// </summary>
        [MenuItem("UMCP/Examples/State-Aware Operation")]
        public static void StateAwareOperation()
        {
            var stateInfo = EditorStateHelper.GetStateDescription();
            Debug.Log($"Current Editor State: {stateInfo}");

            if (!EditorStateHelper.CanModifyProjectFiles)
            {
                EditorUtility.DisplayDialog(
                    "Operation Not Allowed",
                    $"Cannot perform this operation in current state:\n" +
                    $"Runmode: {EditorStateHelper.CurrentRunmode}\n" +
                    $"Context: {EditorStateHelper.CurrentContext}",
                    "OK"
                );
                return;
            }

            // Perform the operation
            Debug.Log("Performing state-aware operation...");
        }

        /// <summary>
        /// Validate menu item based on editor state
        /// </summary>
        [MenuItem("UMCP/Examples/State-Aware Operation", true)]
        public static bool ValidateStateAwareOperation()
        {
            return EditorStateHelper.CanModifyProjectFiles;
        }

        /// <summary>
        /// Example of asset import handling
        /// </summary>
        [MenuItem("UMCP/Examples/Import Test Asset")]
        public static void ImportTestAsset()
        {
            if (EditorStateHelper.CurrentContext == EditorStateHelper.Context.UpdatingAssets)
            {
                Debug.LogWarning("Asset import already in progress. Please wait...");
                return;
            }

            // Create a test asset
            var testAsset = ScriptableObject.CreateInstance<ScriptableObject>();
            AssetDatabase.CreateAsset(testAsset, "Assets/TestAsset.asset");
            AssetDatabase.SaveAssets();
            
            // Force an import with tracking
            AssetDatabaseExtensions.ImportAssetWithTracking("Assets/TestAsset.asset", ImportAssetOptions.ForceUpdate);
            
            Debug.Log($"Asset import triggered. Current context: {EditorStateHelper.CurrentContext}");
        }

        /// <summary>
        /// Example of monitoring asset database operations
        /// </summary>
        public static void PerformAssetDatabaseOperation(System.Action operation)
        {
            if (EditorStateHelper.CurrentContext == EditorStateHelper.Context.UpdatingAssets)
            {
                Debug.Log("Waiting for current asset update to complete...");
                EditorApplication.delayCall += () => PerformAssetDatabaseOperation(operation);
                return;
            }

            Debug.Log("Performing asset database operation...");
            operation?.Invoke();
        }

        /// <summary>
        /// Example of using AssetOperationScope for complex operations
        /// </summary>
        [MenuItem("UMCP/Examples/Batch Asset Operation")]
        public static void BatchAssetOperation()
        {
            using (var scope = new AssetOperationScope())
            {
                Debug.Log("Starting batch asset operation...");
                
                // Multiple asset operations within a single scope
                for (int i = 0; i < 5; i++)
                {
                    var asset = ScriptableObject.CreateInstance<ScriptableObject>();
                    AssetDatabase.CreateAsset(asset, $"Assets/BatchAsset_{i}.asset");
                }
                
                AssetDatabase.SaveAssets();
                Debug.Log("Batch operation complete.");
            }
            // Scope disposed - EditorStateHelper notified of completion
        }
    }
}