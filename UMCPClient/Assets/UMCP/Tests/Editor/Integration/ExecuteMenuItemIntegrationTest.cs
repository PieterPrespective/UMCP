using System.Collections;
using UMCP.Editor.Helpers;
using UMCP.Editor.Testing;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace UMCP.Tests.Editor.Integration
{
    /// <summary>
    /// Integration test harness for ExecuteMenuItem tool.
    /// Creates a test menu item that logs messages when executed, controlled by compile flags.
    /// </summary>
    public class ExecuteMenuItemIntegrationTest : UMCPIntegrationTestHarnass
    {
        /// <summary>
        /// Sets up the integration test by adding the compile flag and forcing editor update.
        /// This will enable the test menu item creation.
        /// </summary>
        public override IEnumerator SetupIntegrationTest()
        {


            yield return null;

            //Disabled the Complile flag flipping - it leads to too much issues with the test harness and the editor.

            //Debug.Log("[ExecuteMenuItemIntegrationTest] Setting up integration test harness");

            //// Add the compile flag to enable the test menu item
            //AddCompileFlag("UMCP_INTEGRATION_TEST_EXECUTE_MENU_ITEM");

            //// Force editor update to apply compile changes
            //Debug.Log("[ExecuteMenuItemIntegrationTest] Compile flag added, requesting editor update");
            //AssetDatabase.Refresh();

            //yield return new WaitForSecondsRealtime(1.0f);  

            //System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            //long maxRecompileTimeout = 120000; // 120 seconds in milliseconds

            //// Wait a moment for the compilation to take effect
            //yield return new WaitUntil(() => (EditorStateHelper.CurrentContext == EditorStateHelper.Context.Running || stopwatch.ElapsedMilliseconds > maxRecompileTimeout));

            //if(stopwatch.ElapsedMilliseconds > maxRecompileTimeout)
            //{
            //    throw new System.TimeoutException("Timeout waiting for editor to recompile after adding compile flag.");
            //}

            //Debug.Log("[ExecuteMenuItemIntegrationTest] Setup completed successfully");
        }

        /// <summary>
        /// Cleans up the integration test by removing the compile flag.
        /// </summary>
        public override IEnumerator CleanupIntegrationTest()
        {
            yield return null;

            //Debug.Log("[ExecuteMenuItemIntegrationTest] Cleaning up integration test harness");

            //// Remove the compile flag to disable the test menu item
            //RemoveCompileFlag("UMCP_INTEGRATION_TEST_EXECUTE_MENU_ITEM");

            //Debug.Log("[ExecuteMenuItemIntegrationTest] Compile flag removed, requesting editor update");
            //AssetDatabase.Refresh();

            //yield return new WaitForSecondsRealtime(1.0f);

            //System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            //long maxRecompileTimeout = 120000; // 120 seconds in milliseconds

            //// Wait a moment for the compilation to take effect
            //yield return new WaitUntil(() => (EditorStateHelper.CurrentContext == EditorStateHelper.Context.Running || stopwatch.ElapsedMilliseconds > maxRecompileTimeout));

            //if (stopwatch.ElapsedMilliseconds > maxRecompileTimeout)
            //{
            //    throw new System.TimeoutException("Timeout waiting for editor to recompile after removing compile flag.");
            //}


            //// Wait a moment for the compilation to take effect
            //yield return new WaitForSecondsRealtime(1.0f);

            //Debug.Log("[ExecuteMenuItemIntegrationTest] Cleanup completed successfully");
        }

        /// <summary>
        /// Adds a compile flag to the current build target group.
        /// </summary>
        /// <param name="flag">The compile flag to add</param>
        public static void AddCompileFlag(string flag)
        {
            // Get the current build target group
            BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);

            // Get current symbols
            string currentSymbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            
            // Check if the flag already exists
            if (!currentSymbols.Contains(flag))
            {
                // Add the new flag (with semicolon separator if there are existing symbols)
                if (!string.IsNullOrEmpty(currentSymbols))
                    currentSymbols += ";";
                currentSymbols += flag;
                
                // Apply the new symbols
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, currentSymbols);
                Debug.Log($"[ExecuteMenuItemIntegrationTest] Added compile flag: {flag}");
            }
            else
            {
                Debug.Log($"[ExecuteMenuItemIntegrationTest] Compile flag already exists: {flag}");
            }
        }

        /// <summary>
        /// Removes a compile flag from the current build target group.
        /// </summary>
        /// <param name="flag">The compile flag to remove</param>
        public static void RemoveCompileFlag(string flag)
        {
            // Get the current build target group
            BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);

            // Get current symbols
            string currentSymbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            
            // Remove the flag if it exists
            if (currentSymbols.Contains(flag))
            {
                // Split symbols, remove the flag, and rejoin
                string[] symbols = currentSymbols.Split(';');
                string newSymbols = string.Join(";", System.Array.FindAll(symbols, s => s != flag));
                
                // Apply the updated symbols
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, newSymbols);
                Debug.Log($"[ExecuteMenuItemIntegrationTest] Removed compile flag: {flag}");
            }
            else
            {
                Debug.Log($"[ExecuteMenuItemIntegrationTest] Compile flag not found: {flag}");
            }
        }
    }

#if UMCP_INTEGRATION_TEST
    /// <summary>
    /// Test menu item creator for ExecuteMenuItem integration testing.
    /// This class is only compiled when the UMCP_INTEGRATION_TEST_EXECUTE_MENU_ITEM flag is defined.
    /// </summary>
    public static class ExecuteMenuItemTestMenuCreator
    {
        /// <summary>
        /// Test menu item that logs a message when executed.
        /// This serves as the target for ExecuteMenuItem integration tests.
        /// </summary>
        [MenuItem("UMCP Integration Tests/Execute Menu Item Test", false, 1)]
        public static void ExecuteMenuItemTest()
        {
            Debug.Log("[UMCP_INTEGRATION_TEST] ExecuteMenuItem test menu item was executed successfully!");
        }

        /// <summary>
        /// Another test menu item for testing multiple menu paths.
        /// </summary>
        [MenuItem("UMCP Integration Tests/Execute Menu Item Test 2", false, 2)]
        public static void ExecuteMenuItemTest2()
        {
            Debug.Log("[UMCP_INTEGRATION_TEST] ExecuteMenuItem test menu item 2 was executed successfully!");
        }

        /// <summary>
        /// Test menu item with submenu for testing nested paths.
        /// </summary>
        [MenuItem("UMCP Integration Tests/Submenu/Execute Menu Item Nested Test", false, 10)]
        public static void ExecuteMenuItemNestedTest()
        {
            Debug.Log("[UMCP_INTEGRATION_TEST] ExecuteMenuItem nested test menu item was executed successfully!");
        }
    }
#endif
}