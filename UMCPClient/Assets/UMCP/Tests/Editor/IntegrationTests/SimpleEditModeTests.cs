#if UMCP_INTEGRATION_TEST

using NUnit.Framework;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace UMCP.editor.integrationtests
{
    /// <summary>
    /// Simple EditMode tests for integration testing
    /// </summary>
    [TestFixture]
    public class SimpleEditModeTests
    {
        /// <summary>
        /// Test that validates basic addition - should pass
        /// </summary>
        [Test]
        public void TestAdditionSucceeds()
        {
            Assert.That(1 + 1, Is.EqualTo(2));
        }
        
        /// <summary>
        /// Test that validates basic subtraction - should pass
        /// </summary>
        [Test]
        public void TestSubstractionSucceeds()
        {
            Assert.That(5 - 1, Is.EqualTo(4));
        }
        
        /// <summary>
        /// Test that contains a faulty assertion - should fail
        /// </summary>
        [Test]
        public void TestAdditionFailure()
        {
            Assert.That(6 + 2, Is.EqualTo(7), "This test is intentionally faulty");
        }

        [UnityTest]
        public IEnumerator TestEditModeIenumeratorSucceeds()
        {
            yield return new WaitForSecondsRealtime(1f);

            Assert.That(3 * 3, Is.EqualTo(9));

            yield return null;
        }

        [UnityTest]
        public IEnumerator TestEditModeIenumeratorFailure()
        {
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(3 * 3, Is.EqualTo(8), "This test is intentionally faulty");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestEnterPlayModeWithDomainReloadSucceeds()
        {
            // Enter Play Mode
            yield return new EnterPlayMode();
            // Simple assertion to verify we are in play mode
            Assert.IsTrue(Application.isPlaying, "Failed to enter Play Mode with domain reload.");
            // Exit Play Mode
            yield return new ExitPlayMode();
            // Simple assertion to verify we are back in edit mode
            Assert.IsFalse(Application.isPlaying, "Failed to exit Play Mode back to Edit Mode.");
        }

        [UnityTest]
        public IEnumerator TestEnterPlayModeWithDomainReloadFails()
        {
            // Enter Play Mode
            yield return new EnterPlayMode();

            // Simple assertion to verify we are in play mode
            Assert.That(3 * 3, Is.EqualTo(8), "This test is intentionally faulty");

            // Exit Play Mode
            yield return new ExitPlayMode();

            // Simple assertion to verify we are back in edit mode
            Assert.IsFalse(Application.isPlaying, "Failed to exit Play Mode back to Edit Mode.");
        }

    }
}
#endif