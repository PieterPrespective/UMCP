using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;
using UMCP.Editor.Testing;
using UMCP.Editor.Models;
using UMCP.Editor.Helpers;
using Unity.EditorCoroutines.Editor;

namespace UMCP.Editor.Tools
{
    /// <summary>
    /// Tool for managing integration tests with real Unity instances.
    /// Provides setup, cleanup, and validation functionality for integration test harnesses.
    /// Uses persistent state storage to survive domain reloads and supports polling architecture.
    /// </summary>
    public static class ManageIntegrationTests
    {
        private static UMCPIntegrationTestHarnass activeHarnass = null;
        private static IntegrationTestStateStorage _stateStorage;
        
        /// <summary>
        /// Gets the persistent state storage instance, creating one if necessary
        /// </summary>
        private static IntegrationTestStateStorage StateStorage
        {
            get
            {
                if (_stateStorage == null)
                {
                    // Try to find existing storage
                    var storages = Resources.FindObjectsOfTypeAll<IntegrationTestStateStorage>();
                    if (storages.Length > 0)
                    {
                        _stateStorage = storages[0];
                    }
                    else
                    {
                        // Create new storage
                        _stateStorage = ScriptableObject.CreateInstance<IntegrationTestStateStorage>();
                        _stateStorage.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
                return _stateStorage;
            }
        }

        /// <summary>
        /// Handles integration test management commands from the server.
        /// </summary>
        /// <param name="parameters">Command parameters containing action and test class information</param>
        /// <returns>Response object with command execution results</returns>
        public static object HandleCommand(JObject parameters)
        {
            try
            {
                if (parameters == null)
                {
                    return Response.Error("Parameters cannot be null");
                }

                string action = parameters["action"]?.ToString();
                if (string.IsNullOrEmpty(action))
                {
                    return Response.Error("Action parameter is required");
                }

                switch (action.ToLower())
                {
                    case "setup":
                        return HandleSetupIntegrationTest(parameters);
                    case "cleanup":
                        return HandleClearIntegrationTest(parameters);
                    case "get_all_harnesses":
                        return GetAllHarnesses();
                    case "get_integration_test_state":
                        return HandleGetIntegrationTestState();
                    default:
                        return Response.Error($"Unknown action: {action}");
                }
            }
            catch (Exception ex)
            {
                return Response.Error($"Integration test management failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets up an integration test by instantiating the specified harness class and running its setup method.
        /// Uses polling architecture - initiates setup and returns immediately, client should poll for completion.
        /// </summary>
        /// <param name="parameters">Parameters containing the harness class name</param>
        /// <returns>Response indicating setup initiation success or failure</returns>
        private static object HandleSetupIntegrationTest(JObject parameters)
        {
            try
            {
                string harnassClassName = parameters["harnassClass"]?.ToString();
                if (string.IsNullOrEmpty(harnassClassName))
                {
                    StateStorage.SetError("harnassClass parameter is required");
                    return Response.Error("harnassClass parameter is required");
                }

                // Check if we're already in a setup or cleanup process
                if (StateStorage.IsInTransition)
                {
                    return Response.Error($"Integration test is currently in transition state: {StateStorage.CurrentTestState}. Please wait or cleanup first.");
                }

                // Set initial state
                StateStorage.BeginSetup(harnassClassName);

                // Cleanup any existing harness first
                if (activeHarnass != null)
                {
                    // Store harness data before cleanup
                    StateStorage.SerializedHarnassData = activeHarnass.OnBeforeSerialize();
                    EditorCoroutineUtility.StartCoroutineOwnerless(CleanupActiveHarnassForNewSetup());
                    activeHarnass = null;
                }

                // Close current scene without saving
                if (SceneManager.sceneCount > 0)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }

                // Find the harness class using reflection
                Type harnassType = FindHarnassType(harnassClassName);
                if (harnassType == null)
                {
                    StateStorage.SetError($"Integration test harness class '{harnassClassName}' not found");
                    StateStorage.CurrentTestState = IntegrationTestStateStorage.TestState.Clean;
                    return Response.Error($"Integration test harness class '{harnassClassName}' not found");
                }

                // Verify the harness is in the correct location (Tests/Editor/Integration folder)
                if (!IsValidHarnassLocation(harnassType))
                {
                    string errorMsg = $"Integration test harness '{harnassClassName}' must be located in the 'UMCP/Tests/Editor/Integration' folder";
                    StateStorage.SetError(errorMsg);
                    StateStorage.CurrentTestState = IntegrationTestStateStorage.TestState.Clean;
                    return Response.Error(errorMsg);
                }

                // Create an instance of the harness
                activeHarnass = Activator.CreateInstance(harnassType) as UMCPIntegrationTestHarnass;
                if (activeHarnass == null)
                {
                    string errorMsg = $"Failed to create instance of harness '{harnassClassName}'";
                    StateStorage.SetError(errorMsg);
                    StateStorage.CurrentTestState = IntegrationTestStateStorage.TestState.Clean;
                    return Response.Error(errorMsg);
                }

                // Restore harness data if any exists
                if (!string.IsNullOrEmpty(StateStorage.SerializedHarnassData))
                {
                    activeHarnass.OnAfterDeserialize(StateStorage.SerializedHarnassData);
                }

                // Create a new empty scene
                Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (!newScene.IsValid())
                {
                    string errorMsg = "Failed to create new scene for integration test";
                    StateStorage.SetError(errorMsg);
                    StateStorage.CurrentTestState = IntegrationTestStateStorage.TestState.Clean;
                    return Response.Error(errorMsg);
                }

                // Start the harness setup coroutine
                EditorCoroutineUtility.StartCoroutineOwnerless(RunHarnassSetup());

                return Response.Success("Integration test setup initiated successfully. Poll with get_integration_test_state to check progress.", new
                {
                    harnassClass = harnassClassName,
                    sceneName = newScene.name,
                    state = StateStorage.CurrentTestState.ToString(),
                    message = "Setup initiated - use polling to monitor progress"
                });
            }
            catch (Exception ex)
            {
                StateStorage.SetError($"Setup integration test failed: {ex.Message}");
                StateStorage.CurrentTestState = IntegrationTestStateStorage.TestState.Clean;
                return Response.Error($"Setup integration test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up the active integration test harness and closes the test scene.
        /// Uses polling architecture - initiates cleanup and returns immediately, client should poll for completion.
        /// </summary>
        /// <param name="parameters">Command parameters (not used for cleanup)</param>
        /// <returns>Response indicating cleanup initiation success or failure</returns>
        private static object HandleClearIntegrationTest(JObject parameters = null)
        {
            try
            {
                // Check current state
                if (StateStorage.CurrentTestState == IntegrationTestStateStorage.TestState.Clean)
                {
                    return Response.Success("Integration test is already in clean state");
                }

                // Check if we're already cleaning
                if (StateStorage.CurrentTestState == IntegrationTestStateStorage.TestState.Cleaning)
                {
                    return Response.Success("Integration test cleanup already in progress. Poll get_integration_test_state to check progress.");
                }

                if (activeHarnass == null && StateStorage.CurrentTestState != IntegrationTestStateStorage.TestState.SetUp)
                {
                    StateStorage.ClearState();
                    return Response.Success("No active integration test harness to clean up");
                }

                // Begin cleanup state
                StateStorage.BeginCleanup();

                // Store harness data before cleanup
                if (activeHarnass != null)
                {
                    StateStorage.SerializedHarnassData = activeHarnass.OnBeforeSerialize();
                }

                // Run cleanup on the active harness
                EditorCoroutineUtility.StartCoroutineOwnerless(CleanupActiveHarnass());

                return Response.Success("Integration test cleanup initiated successfully. Poll with get_integration_test_state to check progress.", new
                {
                    previousState = StateStorage.CurrentTestState.ToString(),
                    currentState = "Cleaning",
                    message = "Cleanup initiated - use polling to monitor progress"
                });
            }
            catch (Exception ex)
            {
                StateStorage.SetError($"Clear integration test failed: {ex.Message}");
                return Response.Error($"Clear integration test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current state of the integration test system.
        /// Used by the server for polling the client to check progress.
        /// </summary>
        /// <returns>Response with current integration test state information</returns>
        private static object HandleGetIntegrationTestState()
        {
            try
            {
                return Response.Success("Integration test state retrieved successfully", new
                {
                    currentLoadedHarnass = StateStorage.CurrentLoadedHarnass,
                    currentTestState = StateStorage.CurrentTestState.ToString(),
                    isTestActive = StateStorage.IsTestActive,
                    isInTransition = StateStorage.IsInTransition,
                    stateChangeTimestamp = StateStorage.StateChangeTimestamp,
                    lastError = StateStorage.LastError,
                    stateDescription = StateStorage.GetStateDescription(),
                    hasActiveHarnassInstance = activeHarnass != null,
                    editorIsResponsive = EditorStateHelper.IsEditorResponsive,
                    canModifyProjectFiles = EditorStateHelper.CanModifyProjectFiles
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to get integration test state: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets all available integration test harnesses using reflection.
        /// </summary>
        /// <returns>Response with information about all harnesses</returns>
        private static object GetAllHarnesses()
        {
            try
            {
                List<Type> harnassTypes = FindAllHarnassTypes();
                List<object> harnessResults = new List<object>();

                foreach (Type harnassType in harnassTypes)
                {
                    try
                    {
                        harnessResults.Add(new
                        {
                            className = harnassType.Name,
                            fullName = harnassType.FullName,
                            location = IsValidHarnassLocation(harnassType) ? "Valid" : "Invalid (not in Tests/Editor/Integration)",
                            isValidLocation = IsValidHarnassLocation(harnassType)
                        });
                    }
                    catch (Exception ex)
                    {
                        harnessResults.Add(new
                        {
                            className = harnassType.Name,
                            fullName = harnassType.FullName,
                            location = "Error",
                            isValidLocation = false,
                            error = ex.Message
                        });
                    }
                }

                return Response.Success($"Found {harnessResults.Count} integration test harnesses", new
                {
                    totalHarnesses = harnessResults.Count,
                    results = harnessResults
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Get all harnesses failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Runs the setup method of the active harness in a coroutine.
        /// Updates state storage to track progress and completion.
        /// </summary>
        /// <returns>Coroutine enumerator</returns>
        private static IEnumerator RunHarnassSetup()
        {
            if (activeHarnass == null)
            {
                StateStorage.SetError("No active harness available for setup");
                StateStorage.CurrentTestState = IntegrationTestStateStorage.TestState.Clean;
                yield break;
            }

            Debug.Log($">>> Running setup for integration test harness: {activeHarnass.GetType().Name}");

            yield return CoroutineErrorCatcherUtility.RunThrowingIterator(activeHarnass.SetupIntegrationTest(), (ex) =>
            {
                if (ex != null)
                {
                    Debug.LogError($"Integration test harness setup failed: {ex.Message}");
                    StateStorage.SetError($"Setup failed: {ex.Message}");
                    StateStorage.CurrentTestState = IntegrationTestStateStorage.TestState.Clean;
                }
                else
                {
                    Debug.Log($">>> Integration test harness setup completed successfully: {activeHarnass.GetType().Name}");
                    StateStorage.CompleteSetup();
                }
            });
        }

        /// <summary>
        /// Runs the cleanup method of the active harness in a coroutine.
        /// Updates state storage to track progress and completion.
        /// </summary>
        /// <returns>Coroutine enumerator</returns>
        private static IEnumerator CleanupActiveHarnass()
        {
            if (activeHarnass == null)
            {
                Debug.Log(">>> No active harness to cleanup");
                CompleteCleanupProcess();
                yield break;
            }

            Debug.Log($">>> Running cleanup for integration test harness: {activeHarnass.GetType().Name}");

            yield return CoroutineErrorCatcherUtility.RunThrowingIterator(activeHarnass.CleanupIntegrationTest(), (ex) =>
            {
                if (ex != null)
                {
                    Debug.LogError($"Integration test harness cleanup failed: {ex.Message}");
                    StateStorage.SetError($"Cleanup failed: {ex.Message}");
                }
                else
                {
                    Debug.Log($">>> Integration test harness cleanup completed successfully: {activeHarnass.GetType().Name}");
                }
                
                // Complete cleanup process regardless of success/failure
                CompleteCleanupProcess();
            });
        }

        /// <summary>
        /// Completes the cleanup process by resetting state and closing scenes.
        /// </summary>
        private static void CompleteCleanupProcess()
        {
            // Close the scene hard without saving
            if (SceneManager.sceneCount > 0)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            activeHarnass = null;
            StateStorage.CompleteCleanup();
        }

        /// <summary>
        /// Cleanup method specifically for when setting up a new harness.
        /// </summary>
        /// <returns>Coroutine enumerator</returns>
        private static IEnumerator CleanupActiveHarnassForNewSetup()
        {
            yield return CleanupActiveHarnass();
        }

        /// <summary>
        /// Finds a specific harness type by class name using reflection.
        /// </summary>
        /// <param name="className">Name of the harness class to find</param>
        /// <returns>Type of the harness class, or null if not found</returns>
        private static Type FindHarnassType(string className)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(type => type.IsSubclassOf(typeof(UMCPIntegrationTestHarnass)) && 
                                      type.Name.Equals(className, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds all available integration test harness types using reflection.
        /// </summary>
        /// <returns>List of all harness types that inherit from UMCPIntegrationTestHarnass</returns>
        private static List<Type> FindAllHarnassTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsSubclassOf(typeof(UMCPIntegrationTestHarnass)) && !type.IsAbstract)
                .ToList();
        }

        /// <summary>
        /// Verifies that a harness type is located in the correct folder (Tests/Editor/Integration).
        /// </summary>
        /// <param name="harnassType">Type to verify location for</param>
        /// <returns>True if in valid location, false otherwise</returns>
        private static bool IsValidHarnassLocation(Type harnassType)
        {
            // Check if the type's assembly or namespace indicates it's in the correct location
            string namespaceName = harnassType.Namespace ?? "";
            return namespaceName.Contains("Tests.Editor") || namespaceName.Contains("Tests") || namespaceName.Contains("Integration");
        }
    }

    /// <summary>
    /// Editor coroutine utility for running coroutines in edit mode.
    /// </summary>
    public static class CoroutineErrorCatcherUtility
    {

        /// <summary>
        /// Run an iterator function that might throw an exception. Call the callback with the exception
        /// if it does or null if it finishes without throwing an exception.
        /// </summary>
        /// <param name="enumerator">Iterator function to run</param>
        /// <param name="done">Callback to call when the iterator has thrown an exception or finished.
        /// The thrown exception or null is passed as the parameter.</param>
        /// <returns>An enumerator that runs the given enumerator</returns>
        public static IEnumerator RunThrowingIterator(
            IEnumerator enumerator,
            Action<Exception> done
        )
        {
            while (true)
            {
                object current;
                try
                {
                    if (enumerator.MoveNext() == false)
                    {
                        break;
                    }
                    current = enumerator.Current;
                }
                catch (Exception ex)
                {
                    done(ex);
                    yield break;
                }
                yield return current;
            }
            done(null);
        }
    }

    }