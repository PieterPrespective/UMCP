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
        private static bool isUpdating = false;
        private static readonly object updateLock = new object();
        public static object HandleCommand(JObject @params)
        {
            try
            {
                // Prevent recursive calls
                lock (updateLock)
                {
                    if (isUpdating)
                    {
                        Debug.LogWarning("[ForceUpdateEditor] Update already in progress, skipping duplicate request");
                        return Response.Success("ForceUpdateEditor skipped - update already in progress", new
                        {
                            skipped = true,
                            reason = "update_already_in_progress"
                        });
                    }
                    isUpdating = true;
                }
                
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
                
                var finalState = new
                {
                    runmode = EditorStateHelper.CurrentRunmode.ToString(),
                    context = EditorStateHelper.CurrentContext.ToString(),
                    timestamp = DateTime.UtcNow.ToString("MM/dd/yyyy HH:mm:ss")
                };
                
                return Response.Success("Unity Editor force update completed successfully", new
                {
                    initialState = new
                    {
                        runmode = currentRunmode.ToString(),
                        context = currentContext.ToString()
                    },
                    finalState = finalState,
                    action = currentRunmode == EditorStateHelper.Runmode.PlayMode ? "exiting_playmode" : "updating_editor",
                    waitTimeMs = 0
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[ForceUpdateEditor] Failed to force update editor: {e}");
                return Response.Error($"Failed to force update editor: {e.Message}");
            }
            finally
            {
                // Always reset the flag
                lock (updateLock)
                {
                    isUpdating = false;
                }
            }
        }
        
        private static void PerformEditorUpdate()
        {
            try
            {
                Debug.Log("[ForceUpdateEditor] Performing editor update operations");


                if(!EditorApplication.isUpdating)
                {
                    // Refresh asset database to ensure all assets are up to date
                    AssetDatabase.Refresh();

                    // Mark scene as dirty to trigger any necessary updates
                    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    if (activeScene.IsValid())
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                    }

                    // Force a repaint of all editor windows using delayCall to avoid recursion
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                            foreach (var window in windows)
                            {
                                if (window != null)
                                {
                                    window.Repaint();
                                    EditorUtility.SetDirty(window);
                                }
                            }
                            // Force Unity to process pending operations
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[ForceUpdateEditor] Error during delayed update: {e}");
                        }
                    };
                }

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
