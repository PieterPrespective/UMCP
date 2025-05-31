using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Compilation;

namespace UMCP.Editor.Helpers
{
    /// <summary>
    /// Helper class for tracking Unity Editor state changes.
    /// Distinguishes between Runmode (EditMode_Scene, EditMode_Prefab, PlayMode) 
    /// and Context (Running, Switching, Compiling, UpdatingAssets).
    /// </summary>
    [InitializeOnLoad]
    public static class EditorStateHelper
    {
        #region Enums
        public enum Runmode
        {
            EditMode_Scene,
            EditMode_Prefab,
            PlayMode
        }

        public enum Context
        {
            Running,
            Switching,
            Compiling,
            UpdatingAssets
        }
        #endregion

        #region State Storage
        

        private static StateStorage _stateStorage;
        private static StateStorage StateStorage
        {
            get
            {
                if (_stateStorage == null)
                {
                    // Try to find existing storage
                    var storages = Resources.FindObjectsOfTypeAll<StateStorage>();
                    if (storages.Length > 0)
                    {
                        _stateStorage = storages[0];
                    }
                    else
                    {
                        // Create new storage
                        _stateStorage = ScriptableObject.CreateInstance<StateStorage>();
                        _stateStorage.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
                return _stateStorage;
            }
        }
        #endregion

        #region Properties
        /// <summary>
        /// Current runmode of the editor
        /// </summary>
        public static Runmode CurrentRunmode
        {
            get => StateStorage.runmode;
            private set
            {
                if (StateStorage.runmode != value)
                {
                    var previous = StateStorage.runmode;
                    StateStorage.runmode = value;
                    OnRunmodeChanged?.Invoke(previous, value);
                }
            }
        }

        /// <summary>
        /// Current context of the editor
        /// </summary>
        public static Context CurrentContext
        {
            get => StateStorage.context;
            private set
            {
                if (StateStorage.context != value)
                {
                    var previous = StateStorage.context;
                    StateStorage.context = value;
                    OnContextChanged?.Invoke(previous, value);
                }
            }
        }

        /// <summary>
        /// Returns true if the editor is in a state where project files can be modified
        /// </summary>
        public static bool CanModifyProjectFiles => 
            CurrentRunmode != Runmode.PlayMode && 
            CurrentContext == Context.Running;

        /// <summary>
        /// Returns true if the editor is currently responsive
        /// </summary>
        public static bool IsEditorResponsive => 
            CurrentContext != Context.Compiling && 
            CurrentContext != Context.UpdatingAssets;
        #endregion

        #region Events
        /// <summary>
        /// Fired when the runmode changes
        /// </summary>
        public static event Action<Runmode, Runmode> OnRunmodeChanged;

        /// <summary>
        /// Fired when the context changes
        /// </summary>
        public static event Action<Context, Context> OnContextChanged;

        /// <summary>
        /// Fired when any state change occurs
        /// </summary>
        public static event Action OnStateChanged;
        #endregion

        #region Initialization
        static EditorStateHelper()
        {
            // Subscribe to Unity Editor events
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.newSceneCreated += OnNewSceneCreated;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssetDatabase.importPackageStarted += OnImportPackageStarted;
            AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
            AssetDatabase.importPackageFailed += OnImportPackageFailed;
            AssetDatabase.importPackageCancelled += OnImportPackageCancelled;

            // Prefab stage events
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;

            // Initial state detection
            DetectCurrentState();
        }
        #endregion

        #region State Detection
        private static void DetectCurrentState()
        {
            // Detect Runmode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                CurrentRunmode = Runmode.PlayMode;
            }
            else
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null)
                {
                    CurrentRunmode = Runmode.EditMode_Prefab;
                    StateStorage.wasInPrefabMode = true;
                }
                else
                {
                    CurrentRunmode = Runmode.EditMode_Scene;
                    StateStorage.wasInPrefabMode = false;
                }
            }

            // Detect Context
            if (EditorApplication.isCompiling)
            {
                CurrentContext = Context.Compiling;
            }
            else if (EditorApplication.isUpdating || StateStorage.IsStillImporting())
            {
                CurrentContext = Context.UpdatingAssets;
            }
            else if (StateStorage.isTransitioning)
            {
                CurrentContext = Context.Switching;
            }
            else
            {
                CurrentContext = Context.Running;
            }
        }

        private static void OnEditorUpdate()
        {
            // Continuously check for state changes
            DetectCurrentState();

            // Check if we've finished transitioning
            if (StateStorage.isTransitioning && CurrentContext == Context.Switching)
            {
                // Check if transition is complete
                if (!EditorApplication.isPlayingOrWillChangePlaymode && 
                    !EditorApplication.isCompiling && 
                    !EditorApplication.isUpdating &&
                    !StateStorage.IsStillImporting())
                {
                    StateStorage.isTransitioning = false;
                    CurrentContext = Context.Running;
                }
            }
        }
        #endregion

        #region Event Handlers
        
        // Play Mode Event Handlers
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                case PlayModeStateChange.ExitingPlayMode:
                    StateStorage.isTransitioning = true;
                    CurrentContext = Context.Switching;
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    CurrentRunmode = StateStorage.wasInPrefabMode ? 
                        Runmode.EditMode_Prefab : Runmode.EditMode_Scene;
                    StateStorage.isTransitioning = false;
                    if (CurrentContext == Context.Switching)
                        CurrentContext = Context.Running;
                    break;

                case PlayModeStateChange.EnteredPlayMode:
                    CurrentRunmode = Runmode.PlayMode;
                    StateStorage.isTransitioning = false;
                    if (CurrentContext == Context.Switching)
                        CurrentContext = Context.Running;
                    break;
            }
            OnStateChanged?.Invoke();
        }

        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            if (CurrentRunmode == Runmode.EditMode_Scene && StateStorage.lastScenePath != scene.path)
            {
                StateStorage.isTransitioning = true;
                CurrentContext = Context.Switching;
                StateStorage.lastScenePath = scene.path;
                
                // Schedule context reset
                EditorApplication.delayCall += () =>
                {
                    if (StateStorage.isTransitioning)
                    {
                        StateStorage.isTransitioning = false;
                        CurrentContext = Context.Running;
                    }
                };
            }
            OnStateChanged?.Invoke();
        }

        private static void OnSceneClosed(UnityEngine.SceneManagement.Scene scene)
        {
            if (CurrentRunmode == Runmode.EditMode_Scene)
            {
                StateStorage.isTransitioning = true;
                CurrentContext = Context.Switching;
            }
            OnStateChanged?.Invoke();
        }

        private static void OnNewSceneCreated(UnityEngine.SceneManagement.Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {
            StateStorage.lastScenePath = scene.path;
            OnStateChanged?.Invoke();
        }

        private static void OnPrefabStageOpened(PrefabStage prefabStage)
        {
            StateStorage.isTransitioning = true;
            CurrentContext = Context.Switching;
            StateStorage.wasInPrefabMode = true;
            
            EditorApplication.delayCall += () =>
            {
                CurrentRunmode = Runmode.EditMode_Prefab;
                StateStorage.isTransitioning = false;
                CurrentContext = Context.Running;
            };
            OnStateChanged?.Invoke();
        }

        private static void OnPrefabStageClosing(PrefabStage prefabStage)
        {
            StateStorage.isTransitioning = true;
            CurrentContext = Context.Switching;
            StateStorage.wasInPrefabMode = false;
            
            EditorApplication.delayCall += () =>
            {
                CurrentRunmode = Runmode.EditMode_Scene;
                StateStorage.isTransitioning = false;
                CurrentContext = Context.Running;
            };
            OnStateChanged?.Invoke();
        }

        private static void OnBeforeAssemblyReload()
        {
            // Save current state before domain reload
            EditorUtility.SetDirty(StateStorage);
        }

        private static void OnAfterAssemblyReload()
        {
            // State is automatically restored from StateStorage
            DetectCurrentState();
            OnStateChanged?.Invoke();
        }

        private static void OnCompilationStarted(object obj)
        {
            CurrentContext = Context.Compiling;
            OnStateChanged?.Invoke();
        }

        private static void OnCompilationFinished(object obj)
        {
            if (CurrentContext == Context.Compiling)
            {
                CurrentContext = StateStorage.isTransitioning ? Context.Switching : Context.Running;
            }
            OnStateChanged?.Invoke();
        }

        private static void OnImportPackageStarted(string packageName)
        {
            CurrentContext = Context.UpdatingAssets;
            OnStateChanged?.Invoke();
        }

        private static void OnImportPackageCompleted(string packageName)
        {
            if (CurrentContext == Context.UpdatingAssets)
            {
                CurrentContext = StateStorage.isTransitioning ? Context.Switching : Context.Running;
            }
            OnStateChanged?.Invoke();
        }

        private static void OnImportPackageFailed(string packageName, string errorMessage)
        {
            if (CurrentContext == Context.UpdatingAssets)
            {
                CurrentContext = StateStorage.isTransitioning ? Context.Switching : Context.Running;
            }
            OnStateChanged?.Invoke();
        }

        private static void OnImportPackageCancelled(string packageName)
        {
            if (CurrentContext == Context.UpdatingAssets)
            {
                CurrentContext = StateStorage.isTransitioning ? Context.Switching : Context.Running;
            }
            OnStateChanged?.Invoke();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Get a string representation of the current state
        /// </summary>
        public static string GetStateDescription()
        {
            return $"Runmode: {CurrentRunmode}, Context: {CurrentContext}, " +
                   $"Can Modify Files: {CanModifyProjectFiles}, " +
                   $"Is Responsive: {IsEditorResponsive}";
        }

        /// <summary>
        /// Force a state refresh
        /// </summary>
        public static void RefreshState()
        {
            DetectCurrentState();
            OnStateChanged?.Invoke();
        }
        
        /// <summary>
        /// Notify that asset importing has started (called by AssetPostprocessor)
        /// </summary>
        internal static void NotifyAssetImportStarted()
        {
            StateStorage.StartAssetImport();
            if (CurrentContext != Context.Compiling)
            {
                CurrentContext = Context.UpdatingAssets;
                OnStateChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Notify that asset importing has completed (called by AssetPostprocessor)
        /// </summary>
        internal static void NotifyAssetImportCompleted()
        {
            StateStorage.EndAssetImport();
            EditorApplication.delayCall += () =>
            {
                RefreshState();
            };
        }
        #endregion
    }
}