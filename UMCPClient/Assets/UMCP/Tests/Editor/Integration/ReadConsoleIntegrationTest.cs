using System.Collections;
using UnityEngine;
using NUnit.Framework;
using UMCP.Editor.Testing;
using UnityEditor;

namespace UMCP.Tests.Editor.Integration
{
    /// <summary>
    /// Integration test harness for testing the ReadConsoleTool functionality.
    /// Creates various types of console messages with numbered identifiers for validation.
    /// </summary>
    public class ReadConsoleIntegrationTest : UMCPIntegrationTestHarnass
    {
        private const string MessagePrefix = "ReadConsoleIntegrationTest";
        private const int MessagesPerType = 5;

        /// <summary>
        /// Sets up the integration test by generating multiple console messages of each type.
        /// Each message contains a number to determine the order of generation.
        /// </summary>
        /// <returns>IEnumerator for Unity coroutine execution</returns>
        public override IEnumerator SetupIntegrationTest()
        {
            Debug.Log($"Starting {MessagePrefix} setup...");

            // Generate Log messages (numbered 1-5)
            for (int i = 1; i <= MessagesPerType; i++)
            {
                Debug.Log($"{MessagePrefix}_Log_{i}: This is log message number {i}");
                yield return null; // Wait one frame between messages
            }

            // Generate Warning messages (numbered 1-5)
            for (int i = 1; i <= MessagesPerType; i++)
            {
                Debug.LogWarning($"{MessagePrefix}_Warning_{i}: This is warning message number {i}");
                yield return null;
            }

            // Generate Error messages (numbered 1-5)
            for (int i = 1; i <= MessagesPerType; i++)
            {
                Debug.LogError($"{MessagePrefix}_Error_{i}: This is error message number {i}");
                yield return null;
            }

            // Generate Exception messages (numbered 1-5)
            for (int i = 1; i <= MessagesPerType; i++)
            {
                try
                {
                    throw new System.Exception($"{MessagePrefix}_Exception_{i}: This is exception message number {i}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }
                yield return null;
            }

            // Generate Assert messages (numbered 1-5)
            for (int i = 1; i <= MessagesPerType; i++)
            {
                Debug.LogAssertion($"{MessagePrefix}_Assert_{i}: This is assertion message number {i}");
                yield return null;
            }

            Debug.Log($"{MessagePrefix} setup completed - Generated {MessagesPerType} messages of each type");
        }

        /// <summary>
        /// Cleans up the integration test. No specific cleanup needed for console messages.
        /// </summary>
        /// <returns>IEnumerator for Unity coroutine execution</returns>
        public override IEnumerator CleanupIntegrationTest()
        {
            Debug.Log($"Starting {MessagePrefix} cleanup...");
            
            // No specific cleanup needed for console messages
            // The messages will remain in the console for validation
            
            yield return null;
            
            Debug.Log($"{MessagePrefix} cleanup completed");
        }

        /// <summary>
        /// Gets the message prefix used for filtering.
        /// </summary>
        public static string GetMessagePrefix()
        {
            return MessagePrefix;
        }

        /// <summary>
        /// Gets the number of messages generated per type.
        /// </summary>
        public static int GetMessagesPerType()
        {
            return MessagesPerType;
        }
    }
}