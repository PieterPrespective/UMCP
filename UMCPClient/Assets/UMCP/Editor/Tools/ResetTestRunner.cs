using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace UMCP.Editor.Tools
{
    /// <summary>
    /// Utility to reset the test runner state when it gets stuck
    /// </summary>
    //[InitializeOnLoad]
    public static class ResetTestRunner
    {
        /////NOTE : DONT ENABLE THIS - test can contain a domain reload which will make the testrunner lose context!
        ///// <summary>
        ///// Static constructor to execute reset immediately on script load
        ///// </summary>
        //static ResetTestRunner()
        //{
        //    EditorApplication.delayCall += () =>
        //    {
        //        if(TestRunnerAPIForwarderUtility.IsRunningTestInUnityTestRunner)
        //        {
        //            Debug.Log("[ResetTestRunner] Test runner is currently running. Skipping automatic reset to avoid interrupting tests.");
        //            return;
        //        }

        //        Debug.Log("[ResetTestRunner] Executing ResetState() method...");
        //        RunTestsUtility.ResetState();
        //        Debug.Log("[ResetTestRunner] Test runner state reset completed!");
        //    };
        //}

        /// <summary>
        /// Menu item to reset the test runner state
        /// </summary>
        [MenuItem("UMCP/Tools/Reset Test Runner State")]
        public static void ResetTestRunnerState()
        {
            Debug.Log("[ResetTestRunner] Resetting test runner state...");
            RunTestsStateUtility.ClearState();
            //RunTestsUtility.ResetState();
            Debug.Log("[ResetTestRunner] Test runner state reset completed!");
        }
    }
}