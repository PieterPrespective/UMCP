using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.AssetImporters;

namespace UMCP.Editor.Helpers
{
    /// <summary>
    /// Asset postprocessor to help detect asset import operations for EditorStateHelper
    /// </summary>
    public class EditorStateAssetPostprocessor : AssetPostprocessor
    {
        private static bool isCurrentlyImporting = false;
        private static int pendingImports = 0;
        private static readonly HashSet<string> currentImportBatch = new HashSet<string>();

        // Called at the very beginning of the import pipeline
        void OnPreprocessAsset()
        {
            if (!isCurrentlyImporting)
            {
                isCurrentlyImporting = true;
                EditorStateHelper.NotifyAssetImportStarted();
            }
            pendingImports++;
            currentImportBatch.Add(assetPath);
        }

        // Called when all assets have finished importing
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // Clear the current batch
            currentImportBatch.Clear();
            pendingImports = 0;
            
            if (isCurrentlyImporting)
            {
                isCurrentlyImporting = false;
                // Delay the notification to ensure all import operations are complete
                EditorApplication.delayCall += () =>
                {
                    EditorStateHelper.NotifyAssetImportCompleted();
                };
            }
        }

        // Specific preprocessors for different asset types
        void OnPreprocessTexture()
        {
            LogImportOperation("Texture");
            NotifyImportStarted();
        }

        void OnPreprocessModel()
        {
            LogImportOperation("Model");
            NotifyImportStarted();
        }

        void OnPreprocessAudio()
        {
            LogImportOperation("Audio");
            NotifyImportStarted();
        }

        void OnPreprocessAnimation()
        {
            LogImportOperation("Animation");
            NotifyImportStarted();
        }

        // Also catch material imports
        void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
        {
            LogImportOperation("Material");
            NotifyImportStarted();
        }

        // Catch prefab imports
        void OnPreprocessPrefab()
        {
            LogImportOperation("Prefab");
            NotifyImportStarted();
        }

        private void LogImportOperation(string assetType)
        {
            if (EditorStateHelper.CurrentContext != EditorStateHelper.Context.UpdatingAssets)
            {
                Debug.Log($"[EditorStateHelper] {assetType} import detected for: {assetPath}");
            }
        }

        private static void NotifyImportStarted()
        {
            if (!isCurrentlyImporting)
            {
                isCurrentlyImporting = true;
                EditorStateHelper.NotifyAssetImportStarted();
            }
        }

        /// <summary>
        /// Manually notify that an asset operation is starting
        /// Can be called by other systems that perform asset operations
        /// </summary>
        public static void BeginAssetOperation()
        {
            if (!isCurrentlyImporting)
            {
                isCurrentlyImporting = true;
                EditorStateHelper.NotifyAssetImportStarted();
            }
        }

        /// <summary>
        /// Manually notify that an asset operation has completed
        /// Should be paired with BeginAssetOperation
        /// </summary>
        public static void EndAssetOperation()
        {
            if (isCurrentlyImporting)
            {
                isCurrentlyImporting = false;
                EditorStateHelper.NotifyAssetImportCompleted();
            }
        }
    }

    /// <summary>
    /// Helper class to ensure asset operations are properly tracked
    /// </summary>
    public class AssetOperationScope : System.IDisposable
    {
        public AssetOperationScope()
        {
            EditorStateAssetPostprocessor.BeginAssetOperation();
        }

        public void Dispose()
        {
            EditorStateAssetPostprocessor.EndAssetOperation();
        }
    }

    /// <summary>
    /// Extension methods for AssetDatabase operations with state tracking
    /// </summary>
    public static class AssetDatabaseExtensions
    {
        /// <summary>
        /// Import an asset with proper state tracking
        /// </summary>
        public static void ImportAssetWithTracking(string path, ImportAssetOptions options = ImportAssetOptions.Default)
        {
            using (new AssetOperationScope())
            {
                AssetDatabase.ImportAsset(path, options);
            }
        }

        /// <summary>
        /// Refresh the asset database with proper state tracking
        /// </summary>
        public static void RefreshWithTracking(ImportAssetOptions options = ImportAssetOptions.Default)
        {
            using (new AssetOperationScope())
            {
                AssetDatabase.Refresh(options);
            }
        }

        /// <summary>
        /// Save assets with proper state tracking
        /// </summary>
        public static bool SaveAssetsWithTracking()
        {
            using (new AssetOperationScope())
            {
                AssetDatabase.SaveAssets();
                
            }
            return true;
        }
    }
}