using System.Collections;
using UnityEngine;
using NUnit.Framework;

namespace UMCP.Editor.Testing
{
    /// <summary>
    /// Abstract base class for integration test harnesses.
    /// Provides setup and cleanup functionality for integration tests that run with a live Unity instance.
    /// Supports serialization for cross-domain reload persistence.
    /// </summary>
    public abstract class UMCPIntegrationTestHarnass
    {
        /// <summary>
        /// Sets up the integration test environment.
        /// This method is called when the server initiates an integration test setup.
        /// Can contain NUnit Assert calls for validation.
        /// </summary>
        /// <returns>IEnumerator for Unity coroutine execution</returns>
        public abstract IEnumerator SetupIntegrationTest();

        /// <summary>
        /// Cleans up after the integration test.
        /// This method is called to clean up the test environment and remove artifacts.
        /// Can contain NUnit Assert calls for validation.
        /// </summary>
        /// <returns>IEnumerator for Unity coroutine execution</returns>
        public abstract IEnumerator CleanupIntegrationTest();


        /// <summary>
        /// Serializes the harness data for persistence across domain reloads.
        /// Called by the IntegrationTestStateStorage before serialization.
        /// Override to persist harness-specific state data.
        /// </summary>
        /// <returns>JSON string containing serialized harness data</returns>
        public virtual string OnBeforeSerialize()
        {
            // Default implementation returns empty string
            // Derived classes should override to serialize their specific state
            return "";
        }

        /// <summary>
        /// Deserializes the harness data after domain reloads.
        /// Called by the ManageIntegrationTests tool when recreating harness instances.
        /// Override to restore harness-specific state data.
        /// </summary>
        /// <param name="serializedData">JSON string containing serialized harness data</param>
        public virtual void OnAfterDeserialize(string serializedData)
        {
            // Default implementation does nothing
            // Derived classes should override to deserialize their specific state
        }
    }
}