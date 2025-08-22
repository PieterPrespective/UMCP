using System.Collections;
using UnityEngine;
using NUnit.Framework;
using UMCP.Editor.Testing;

namespace UMCP.Tests.Editor.Integration
{
    /// <summary>
    /// Dummy integration test harness for testing the integration test infrastructure.
    /// Creates a test cube and logs messages to demonstrate the integration test flow.
    /// </summary>
    public class DummyIntegrationTest : UMCPIntegrationTestHarnass
    {
        private GameObject testCube;
        private const string TestLogMessage = "DummyIntegrationTest setup completed successfully";

        /// <summary>
        /// Sets up the dummy integration test by creating a test cube and logging a message.
        /// </summary>
        /// <returns>IEnumerator for Unity coroutine execution</returns>
        public override IEnumerator SetupIntegrationTest()
        {
            Debug.Log("Starting DummyIntegrationTest setup...");

            // Create a test cube in the scene
            testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testCube.name = "DummyIntegrationTest_TestCube";
            testCube.transform.position = Vector3.zero;

            // Validate the cube was created properly
            Assert.That(testCube, Is.Not.Null, "Test cube should be created successfully");
            Assert.That(testCube.name, Is.EqualTo("DummyIntegrationTest_TestCube"), "Test cube should have correct name");

            yield return null; // Wait one frame

            // Log the completion message that the server-side test will look for
            Debug.Log(TestLogMessage);

            Debug.Log("DummyIntegrationTest setup completed");
        }

        /// <summary>
        /// Cleans up the dummy integration test by destroying the test cube.
        /// </summary>
        /// <returns>IEnumerator for Unity coroutine execution</returns>
        public override IEnumerator CleanupIntegrationTest()
        {
            Debug.Log("Starting DummyIntegrationTest cleanup...");

            // Destroy the test cube if it exists
            if (testCube != null)
            {
                Object.DestroyImmediate(testCube);
                testCube = null;
                Debug.Log("Test cube destroyed");
            }

            yield return null; // Wait one frame

            Debug.Log("DummyIntegrationTest cleanup completed");
        }

        /// <summary>
        /// Gets the expected log message for server-side validation.
        /// </summary>
        public static string GetExpectedLogMessage()
        {
            return TestLogMessage;
        }
    }
}