using System;
using UnityEngine;

namespace UMCP.Editor.Helpers
{
    /// <summary>
    /// Persistent state storage that survives domain reloads
    /// </summary>
    [Serializable]
    public class StateStorage : ScriptableObject
    {
        public EditorStateHelper.Runmode runmode = EditorStateHelper.Runmode.EditMode_Scene;
        public EditorStateHelper.Context context = EditorStateHelper.Context.Running;
        public bool isTransitioning = false;
        public string lastScenePath = "";
        public bool wasInPrefabMode = false;
        public bool isImportingAssets = false;
        public float lastImportTime = 0f;
        
        /// <summary>
        /// Mark that asset importing has started
        /// </summary>
        public void StartAssetImport()
        {
            isImportingAssets = true;
            lastImportTime = (float)UnityEditor.EditorApplication.timeSinceStartup;
        }
        
        /// <summary>
        /// Mark that asset importing has completed
        /// </summary>
        public void EndAssetImport()
        {
            isImportingAssets = false;
        }
        
        /// <summary>
        /// Check if we should still be in importing state (timeout after 2 seconds)
        /// </summary>
        public bool IsStillImporting()
        {
            if (!isImportingAssets) return false;
            
            float currentTime = (float)UnityEditor.EditorApplication.timeSinceStartup;
            float elapsed = currentTime - lastImportTime;
            
            // Timeout after 2 seconds to prevent getting stuck
            if (elapsed > 2.0f)
            {
                isImportingAssets = false;
                return false;
            }
            
            return true;
        }
    }
}