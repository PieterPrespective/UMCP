using NUnit.Framework;
using UMCP.Editor.Helpers;
using UnityEditor.TestTools.TestRunner.Api;
using System.Collections;
using UnityEngine.TestTools;

namespace UMCP.Tests.Editor
{
    /// <summary>
    /// Tests for validating that EditorStateHelper correctly detects Testing context while tests are running
    /// </summary>
    public class EditorStateHelperTestingContextTests
    {
        private EditorStateHelper.Context? capturedContext = null;
        private bool wasTestingContextDetected = false;

        [SetUp]
        public void Setup()
        {
            capturedContext = null;
            wasTestingContextDetected = false;
        }

        /// <summary>
        /// Test that validates the EditorStateHelper shows Testing context while running
        /// </summary>
        [Test]
        public void TestEditorStateHelperShowsTestingContext()
        {
            // Capture the current context immediately
            capturedContext = EditorStateHelper.CurrentContext;
            
            // If we're running this test, we should be in Testing context
            Assert.AreEqual(EditorStateHelper.Context.Testing, capturedContext.Value, 
                "EditorStateHelper should show Testing context while tests are running");
            
            // Mark that we successfully detected the Testing context
            wasTestingContextDetected = true;
        }

        /// <summary>
        /// Unity Test with coroutine to check context over multiple frames
        /// </summary>
        [UnityTest]
        public IEnumerator TestEditorStateHelperShowsTestingContextOverTime()
        {
            // Check initial state
            var initialContext = EditorStateHelper.CurrentContext;
            Assert.AreEqual(EditorStateHelper.Context.Testing, initialContext, 
                "Initial context should be Testing");

            // Wait a frame
            yield return null;

            // Check again after a frame
            var afterFrameContext = EditorStateHelper.CurrentContext;
            Assert.AreEqual(EditorStateHelper.Context.Testing, afterFrameContext, 
                "Context should still be Testing after waiting a frame");

            // Subscribe to state changes to monitor if it changes during test
            bool contextChangedDuringTest = false;
            EditorStateHelper.Context? newContext = null;
            
            void OnContextChanged(EditorStateHelper.Context oldContext, EditorStateHelper.Context context)
            {
                contextChangedDuringTest = true;
                newContext = context;
            }

            EditorStateHelper.OnContextChanged += OnContextChanged;

            // Wait another frame
            yield return null;

            // Clean up event subscription
            EditorStateHelper.OnContextChanged -= OnContextChanged;

            // The context should not have changed during the test
            Assert.IsFalse(contextChangedDuringTest, 
                $"Context should not change during test execution. Changed to: {newContext}");

            // Final check
            var finalContext = EditorStateHelper.CurrentContext;
            Assert.AreEqual(EditorStateHelper.Context.Testing, finalContext, 
                "Final context should still be Testing");
        }

        /// <summary>
        /// Test that the IsEditorResponsive property works correctly during Testing context
        /// </summary>
        [Test]
        public void TestIsEditorResponsiveDuringTesting()
        {
            // During Testing context, the editor should be considered responsive
            Assert.IsTrue(EditorStateHelper.IsEditorResponsive,
                "Editor should be considered responsive during Testing context");
        }

        /// <summary>
        /// Test that CanModifyProjectFiles property works correctly during Testing context
        /// </summary>
        [Test]
        public void TestCanModifyProjectFilesDuringTesting()
        {
            // Get current runmode
            var currentRunmode = EditorStateHelper.CurrentRunmode;
            
            // During Testing context in EditMode, we should be able to modify project files
            if (currentRunmode != EditorStateHelper.Runmode.PlayMode)
            {
                // In EditMode tests, we can modify files even during Testing context
                Assert.IsTrue(EditorStateHelper.CanModifyProjectFiles,
                    "Should be able to modify project files during EditMode tests");
            }
            else
            {
                // In PlayMode tests, we cannot modify files
                Assert.IsFalse(EditorStateHelper.CanModifyProjectFiles,
                    "Should not be able to modify project files during PlayMode tests");
            }
        }

        /// <summary>
        /// Test the GetStateDescription method includes Testing context
        /// </summary>
        [Test]
        public void TestGetStateDescriptionIncludesTestingContext()
        {
            var stateDescription = EditorStateHelper.GetStateDescription();
            
            // The description should mention Testing context
            Assert.IsTrue(stateDescription.Contains("Testing"), 
                $"State description should include 'Testing' context. Actual: {stateDescription}");
        }
    }
}