using System;
using UnityEngine;
using UMCP.Editor.Testing;

namespace UMCP.Editor.Tools
{
    /// <summary>
    /// Persistent state storage for integration tests that survives domain reloads.
    /// Stores current harness state, test progression, and serialized harness data.
    /// </summary>
    [Serializable]
    public class IntegrationTestStateStorage : ScriptableObject, ISerializationCallbackReceiver
    {
        #region Enums
        /// <summary>
        /// Current state of the integration test
        /// </summary>
        public enum TestState
        {
            Clean,
            SettingUp,
            SetUp,
            Cleaning
        }
        #endregion

        #region Fields
        [SerializeField] private string currentLoadedHarnass = "";
        [SerializeField] private TestState currentTestState = TestState.Clean;
        [SerializeField] private string serializedHarnassData = "";
        [SerializeField] private float stateChangeTimestamp = 0f;
        [SerializeField] private string lastError = "";
        #endregion

        #region Properties
        /// <summary>
        /// Name of the currently loaded harness class
        /// </summary>
        public string CurrentLoadedHarnass
        {
            get => currentLoadedHarnass;
            set
            {
                currentLoadedHarnass = value;
                UpdateTimestamp();
            }
        }

        /// <summary>
        /// Current state of the integration test
        /// </summary>
        public TestState CurrentTestState
        {
            get => currentTestState;
            set
            {
                currentTestState = value;
                UpdateTimestamp();
            }
        }

        /// <summary>
        /// Serialized harness data for cross-domain communication
        /// </summary>
        public string SerializedHarnassData
        {
            get => serializedHarnassData;
            set => serializedHarnassData = value;
        }

        /// <summary>
        /// Timestamp of the last state change
        /// </summary>
        public float StateChangeTimestamp => stateChangeTimestamp;

        /// <summary>
        /// Last error message that occurred
        /// </summary>
        public string LastError
        {
            get => lastError;
            set
            {
                lastError = value;
                UpdateTimestamp();
            }
        }

        /// <summary>
        /// Returns true if the integration test is currently active (not in Clean state)
        /// </summary>
        public bool IsTestActive => currentTestState != TestState.Clean;

        /// <summary>
        /// Returns true if the test is in a transitional state (SettingUp or Cleaning)
        /// </summary>
        public bool IsInTransition => currentTestState == TestState.SettingUp || currentTestState == TestState.Cleaning;
        #endregion

        #region State Management
        /// <summary>
        /// Clears all test state and resets to Clean state
        /// </summary>
        public void ClearState()
        {
            currentLoadedHarnass = "";
            currentTestState = TestState.Clean;
            serializedHarnassData = "";
            lastError = "";
            UpdateTimestamp();
        }

        /// <summary>
        /// Sets the state to SettingUp for the specified harness
        /// </summary>
        /// <param name="harnassClassName">Name of the harness class being set up</param>
        public void BeginSetup(string harnassClassName)
        {
            currentLoadedHarnass = harnassClassName;
            currentTestState = TestState.SettingUp;
            lastError = "";
            UpdateTimestamp();
        }

        /// <summary>
        /// Sets the state to SetUp, indicating setup is complete
        /// </summary>
        public void CompleteSetup()
        {
            if (currentTestState == TestState.SettingUp)
            {
                currentTestState = TestState.SetUp;
                UpdateTimestamp();
            }
        }

        /// <summary>
        /// Sets the state to Cleaning, indicating cleanup has begun
        /// </summary>
        public void BeginCleanup()
        {
            currentTestState = TestState.Cleaning;
            UpdateTimestamp();
        }

        /// <summary>
        /// Completes cleanup and resets to Clean state
        /// </summary>
        public void CompleteCleanup()
        {
            ClearState();
        }

        /// <summary>
        /// Records an error and updates the timestamp
        /// </summary>
        /// <param name="error">Error message to record</param>
        public void SetError(string error)
        {
            lastError = error;
            UpdateTimestamp();
        }

        private void UpdateTimestamp()
        {
            stateChangeTimestamp = (float)UnityEditor.EditorApplication.timeSinceStartup;
        }
        #endregion

        #region ISerializationCallbackReceiver Implementation
        /// <summary>
        /// Called before Unity serializes the object.
        /// Triggers OnBeforeSerialize on the active harness if available.
        /// </summary>
        public void OnBeforeSerialize()
        {
            // The harness serialization will be handled by the ManageIntegrationTests tool
            // when it has an active harness instance
        }

        /// <summary>
        /// Called after Unity deserializes the object.
        /// Triggers OnAfterDeserialize on the active harness if available.
        /// </summary>
        public void OnAfterDeserialize()
        {
            // The harness deserialization will be handled by the ManageIntegrationTests tool
            // when it recreates the harness instance from stored data
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Gets a string representation of the current state
        /// </summary>
        /// <returns>String description of current integration test state</returns>
        public string GetStateDescription()
        {
            return $"Harness: {currentLoadedHarnass}, State: {currentTestState}, " +
                   $"IsActive: {IsTestActive}, InTransition: {IsInTransition}, " +
                   $"LastChange: {stateChangeTimestamp:F2}s" +
                   (string.IsNullOrEmpty(lastError) ? "" : $", Error: {lastError}");
        }
        #endregion
    }
}