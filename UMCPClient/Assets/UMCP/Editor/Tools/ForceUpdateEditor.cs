using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using UMCP.Editor.Helpers; // For Response class

namespace UMCP.Editor.Tools
{
    /// <summary>
    /// Handles forced Unity Editor updates to ensure the editor is responsive and in EditMode.
    /// </summary>
    public static class ForceUpdateEditor
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                Debug.Log("[ForceUpdateEditor] Starting editor update process");
                
                // Check current state
                var currentRunmode = EditorStateHelper.CurrentRunmode;
                var currentContext = EditorStateHelper.CurrentContext;
                
                Debug.Log($"[ForceUpdateEditor] Current state: {currentRunmode}, {currentContext}");
                
                // If in PlayMode, exit to EditMode first
                if (currentRunmode == EditorStateHelper.Runmode.PlayMode)
                {
                    Debug.Log("[ForceUpdateEditor] Exiting play mode");
                    EditorApplication.isPlaying = false;
                    
                    // Mark that we're transitioning
                    EditorApplication.delayCall += () =>
                    {
                        PerformEditorUpdate();
                    };
                }
                else
                {
                    // Already in edit mode, just perform the update
                    PerformEditorUpdate();
                }
                
                return Response.Success("ForceUpdateEditor initiated successfully.", new
                {
                    initialRunmode = currentRunmode.ToString(),
                    initialContext = currentContext.ToString(),
                    action = currentRunmode == EditorStateHelper.Runmode.PlayMode ? "exiting_playmode" : "updating_editor"
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[ForceUpdateEditor] Failed to force update editor: {e}");
                return Response.Error($"Failed to force update editor: {e.Message}");
            }
        }
        
        private static void PerformEditorUpdate()
        {
            try
            {
                Debug.Log("[ForceUpdateEditor] Performing editor update operations");

                // Force Unity Editor to update regardless of focus
                EditorApplication.update();
                
                // Refresh asset database to ensure all assets are up to date
                AssetDatabase.Refresh();
                
                // Mark scene as dirty to trigger any necessary updates
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (activeScene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                }
                
                // Force a repaint of all editor windows
                EditorApplication.delayCall += () =>
                {
                    var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                    foreach (var window in windows)
                    {
                        if (window != null)
                        {
                            window.Repaint();
                        }
                    }
                };
                
                // Refresh the state helper to ensure we have the latest state
                EditorStateHelper.RefreshState();
                
                Debug.Log("[ForceUpdateEditor] Editor update operations completed");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ForceUpdateEditor] Error during editor update: {e}");
            }
        }
    }
}
