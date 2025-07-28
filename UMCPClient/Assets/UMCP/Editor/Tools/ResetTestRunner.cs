using UnityEngine;
using UnityEditor;

namespace UMCP.Editor.Tools
{
    /// <summary>
    /// Utility to reset the test runner state when it gets stuck
    /// </summary>
    [InitializeOnLoad]
    public static class ResetTestRunner
    {
        /// <summary>
        /// Static constructor to execute reset immediately on script load
        /// </summary>
        static ResetTestRunner()
        {
            EditorApplication.delayCall += () =>
            {
                Debug.Log("[ResetTestRunner] Executing ResetState() method...");
                RunTestsUtility.ResetState();
                Debug.Log("[ResetTestRunner] Test runner state reset completed!");
            };
        }
        
        /// <summary>
        /// Menu item to reset the test runner state
        /// </summary>
        [MenuItem("UMCP/Tools/Reset Test Runner State")]
        public static void ResetTestRunnerState()
        {
            Debug.Log("[ResetTestRunner] Resetting test runner state...");
            RunTestsUtility.ResetState();
            Debug.Log("[ResetTestRunner] Test runner state reset completed!");
        }
    }
}