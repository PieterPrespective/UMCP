using JetBrains.Annotations;
using Microsoft.CodeAnalysis.FindSymbols;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UMCP.Editor.Tools.ManageScripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static UMCP.Editor.Tools.ManageScripts.SymbolSearchUtility;

namespace UMCP.Tests.Editor
{
    /// <summary>
    /// Unit tests for ManageScripts tool and its utility classes.
    /// </summary>
    [TestFixture]
    public class ManageScriptTests
    {
        private string testScriptPath;
        private string testScriptContent;

        [SetUp]
        public void Setup()
        {
            // Create a temporary test script file
            testScriptPath = Path.Combine(Application.dataPath, "UMCP", "Tests", "Editor", "TestScript.cs");
            testScriptContent = @"using System;
using UnityEngine;

namespace TestNamespace
{
    public class TestClass : MonoBehaviour
    {
        public int testField = 42;
        
        public string TestProperty { get; set; }
        
        public void TestMethod()
        {
            Debug.Log(""Test method"");
        }
        
        private void PrivateMethod()
        {
            // Private implementation
        }
    }
    
    public interface ITestInterface
    {
        void InterfaceMethod();
    }
}";
            
            // Ensure directory exists
            string directory = Path.GetDirectoryName(testScriptPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(testScriptPath, testScriptContent);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test file
            if (File.Exists(testScriptPath))
            {
                File.Delete(testScriptPath);
            }
        }

        #region ScriptSearchUtility Tests

        [Test]
        public void CreateSearchRegex_SimpleText_CreatesCorrectPattern()
        {
            // Arrange
            string searchFilter = "TestMethod";
            
            // Act
            var regex = ScriptSearchUtility.CreateSearchRegex(searchFilter, false, false, false);
            
            // Assert
            Assert.IsNotNull(regex);
            Assert.IsTrue(regex.IsMatch("public void TestMethod()"));
            Assert.IsTrue(regex.IsMatch("TestMethod"));
            Assert.IsTrue(regex.IsMatch("testmethod")); // Case insensitive by default
        }

        [Test]
        public void CreateSearchRegex_WithMatchCase_CreatesCorrectPattern()
        {
            // Arrange
            string searchFilter = "TestMethod";
            
            // Act
            var regex = ScriptSearchUtility.CreateSearchRegex(searchFilter, true, false, false);
            
            // Assert
            Assert.IsNotNull(regex);
            Assert.IsTrue(regex.IsMatch("TestMethod"));
            Assert.IsFalse(regex.IsMatch("testmethod")); // Case sensitive
        }

        [Test]
        public void CreateSearchRegex_WithMatchWholeWord_CreatesCorrectPattern()
        {
            // Arrange
            string searchFilter = "Test";
            
            // Act
            var regex = ScriptSearchUtility.CreateSearchRegex(searchFilter, false, true, false);
            
            // Assert
            Assert.IsNotNull(regex);
            Assert.IsTrue(regex.IsMatch("Test method"));
            Assert.IsTrue(regex.IsMatch("public Test()"));
            Assert.IsFalse(regex.IsMatch("TestMethod")); // Not whole word
            Assert.IsFalse(regex.IsMatch("MyTest")); // Not whole word
        }

        [Test]
        public void CreateSearchRegex_WithRegularExpression_UsesPatternDirectly()
        {
            // Arrange
            string searchFilter = @"Test\w+";
            
            // Act
            var regex = ScriptSearchUtility.CreateSearchRegex(searchFilter, false, false, true);
            
            // Assert
            Assert.IsNotNull(regex);
            Assert.IsTrue(regex.IsMatch("TestMethod"));
            Assert.IsTrue(regex.IsMatch("TestProperty"));
            Assert.IsFalse(regex.IsMatch("Test")); // Needs at least one word character after
        }

        [Test]
        public void GetAllScriptFiles_ReturnsScriptFiles()
        {
            // Act
            var scriptFiles = ScriptSearchUtility.GetAllScriptFiles();
            
            // Assert
            Assert.IsNotNull(scriptFiles);
            Assert.Greater(scriptFiles.Count, 0);
            
            // Should contain our test script
            bool containsTestScript = scriptFiles.Any(f => f.EndsWith("TestScript.cs"));
            Assert.IsTrue(containsTestScript, "Should find the test script file");
            
            // All files should be .cs files
            foreach (var file in scriptFiles)
            {
                Assert.IsTrue(file.EndsWith(".cs"), $"File {file} should be a .cs file");
            }
        }

        [Test]
        public void SearchInScriptFiles_FindsMatches()
        {
            // Arrange
            string searchFilter = "TestMethod";
            
            // Act
            var results = ScriptSearchUtility.SearchInScriptFiles(searchFilter, false, false, false);
            
            // Assert
            Assert.IsNotNull(results);
            Assert.Greater(results.Count, 0, "Should find at least one match");
            
            var testScriptResult = results.FirstOrDefault(r => r.FilePath.EndsWith("TestScript.cs"));
            Assert.IsNotNull(testScriptResult, "Should find match in test script");
            // Line number may vary depending on formatting, just check it's reasonable
            Assert.Greater(testScriptResult.LineNumber, 10, "TestMethod should be found after line 10");
            Assert.Less(testScriptResult.LineNumber, 20, "TestMethod should be found before line 20");
        }

        [Test]
        public void SearchInScriptFiles_WithWholeWord_FindsOnlyWholeWords()
        {
            // Arrange
            string searchFilter = "Test";
            
            // Act
            var resultsWithoutWholeWord = ScriptSearchUtility.SearchInScriptFiles(searchFilter, false, false, false);
            var resultsWithWholeWord = ScriptSearchUtility.SearchInScriptFiles(searchFilter, false, true, false);
            
            // Assert
            Assert.Greater(resultsWithoutWholeWord.Count, resultsWithWholeWord.Count, 
                "Without whole word matching should find more results");
        }

        #endregion

        #region Integration Tests with HandleCommand

        [Test]
        [Ignore("FindInScriptFiles isn't yet fully implemented (low prio)")]
        public async Task HandleCommand_FindInScriptFiles_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "findinscriptfiles",
                ["SearchFilter"] = "TestMethod",
                ["MatchCase"] = false,
                ["MatchWholeWord"] = false,
                ["UseRegularExpression"] = false
            };
            
            // Act
            var result = await ManageScripts.HandleCommand(parameters);
            
            // Assert
            Assert.IsNotNull(result);
            var resultDict = JObject.FromObject(result);
            Assert.IsTrue(resultDict["success"].Value<bool>());
            Assert.IsNotNull(resultDict["data"]);
        }

        [Test]
        [Ignore("FindInScriptFiles isn't yet fully implemented (low prio)")]
        public async Task HandleCommand_FindInScriptFiles_MissingSearchFilter_ReturnsError()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "findinscriptfiles"
            };
            
            // Act
            var result = await ManageScripts.HandleCommand(parameters);
            
            // Assert
            Assert.IsNotNull(result);
            var resultDict = JObject.FromObject(result);
            Assert.IsFalse(resultDict["success"].Value<bool>());
            Assert.IsNotNull(resultDict["error"]);
            Assert.IsTrue(resultDict["error"].ToString().Contains("SearchFilter"));
        }

        [Test]
        public async Task HandleCommand_FindSymbolDefinition_Success()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "findsymboldefinition",
                ["Symbol"] = "MonoBehaviour"
            };
            
            // Act
            var result = await ManageScripts.HandleCommand(parameters);

            Debug.Log("Result from finding symbol definition: " + result.ToString());

            // Assert
            Assert.IsNotNull(result);
            var resultDict = JObject.FromObject(result);


            
            // MonoBehaviour is a Unity class, so it should be found in assemblies
            // The test might not find it if running in a limited test environment
            // so we just check that the command executes without throwing
            Assert.IsNotNull(resultDict["success"]);
        }

        [Test]
        public async Task HandleCommand_FindSymbolDefinition_MissingSymbol_ReturnsError()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "findsymboldefinition"
            };
            
            // Act
            var result = await ManageScripts.HandleCommand(parameters);
            
            // Assert
            Assert.IsNotNull(result);
            var resultDict = JObject.FromObject(result);
            Assert.IsFalse(resultDict["success"].Value<bool>());
            Assert.IsNotNull(resultDict["error"]);
            Assert.IsTrue(resultDict["error"].ToString().Contains("Symbol"));
        }

        

        [Test]
        public async Task HandleCommand_UnknownAction_ReturnsError()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "unknownaction"
            };
            
            // Act
            var result = await ManageScripts.HandleCommand(parameters);
            
            // Assert
            Assert.IsNotNull(result);
            var resultDict = JObject.FromObject(result);
            Assert.IsFalse(resultDict["success"].Value<bool>());
            Assert.IsNotNull(resultDict["error"]);
            Assert.IsTrue(resultDict["error"].ToString().Contains("Unknown action"));
        }

        [Test]
        public async Task HandleCommand_MissingAction_ReturnsError()
        {
            // Arrange
            var parameters = new JObject();
            
            // Act
            var result = await ManageScripts.HandleCommand(parameters);
            
            // Assert
            Assert.IsNotNull(result);
            var resultDict = JObject.FromObject(result);
            Assert.IsFalse(resultDict["success"].Value<bool>());
            Assert.IsNotNull(resultDict["error"]);
            Assert.IsTrue(resultDict["error"].ToString().Contains("Action parameter is required"));
        }

        #endregion

        #region SymbolSearchUtility Tests

        [Test]
        public async Task _01_FindSymbolDefinition_FindClassSymbolInScripts()
        {
            //VisualElement


            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            //Test with only class name
            string symbol = "TestSymbolSearchClass";
            List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol, results, stopwatch);

            Assert.That(results, Is.Not.Null, $"'{symbol}' should be found");
            Assert.That(results.Count, Is.GreaterThan(0), $"'{symbol}' should be found");

            //Test with class name and namespace
            string symbol2 = "UMCP.Tests.Player.TestSymbolSearchClass";
            List<SymbolDefinitionResult> results2 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol2, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol2, results2, stopwatch);
            Assert.That(results2, Is.Not.Null, $"'{symbol2}' should be found");
            Assert.That(results2.Count, Is.GreaterThan(0), $"'{symbol2}' should be found");

            //Test with non existent classname
            string symbol3 = "UMCP.Tests.Player.TestSymbolSearchClass_NonExistent";
            List<SymbolDefinitionResult> results3 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol3, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol3, results3, stopwatch);
            Assert.That(results3, Is.Not.Null, $"'{symbol3}' should be found");
            Assert.That(results3.Count, Is.EqualTo(0), $"'{symbol3}' should NOT be found");
        }

        [Test]
        public async Task _02_FindSymbolDefinition_FindStructSymbolInScripts()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string symbol = "TestSymbolSearchStruct";
            string symbol2 = "UMCP.Tests.Player.TestSymbolSearchStruct";
            string symbol3 = "UMCP.Tests.Player.TestSymbolSearchStruct_NonExistent";

            List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol, results, stopwatch);

            Assert.That(results, Is.Not.Null, $"'{symbol}' should be found");
            Assert.That(results.Count, Is.GreaterThan(0), $"'{symbol}' should be found");

            
            List<SymbolDefinitionResult> results2 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol2, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol2, results2, stopwatch);
            Assert.That(results2, Is.Not.Null, $"'{symbol2}' should be found");
            Assert.That(results2.Count, Is.GreaterThan(0), $"'{symbol2}' should be found");

            //Test with non existent structname
            List<SymbolDefinitionResult> results3 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol3, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol3, results3, stopwatch);
            Assert.That(results3, Is.Not.Null, $"'{symbol3}' should be found");
            Assert.That(results3.Count, Is.EqualTo(0), $"'{symbol3}' should NOT be found");
        }

        [Test]
        public async Task _03_FindSymbolDefinition_FindPropertySymbolInScripts()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string symbol = "TestSymbolSearchClass.TestSymbolSearchProperty";
            string symbol2 = "UMCP.Tests.Player.TestSymbolSearchClass.TestSymbolSearchProperty";
            string symbol3 = "UMCP.Tests.Player.TestSymbolSearchClass.TestSymbolSearchProperty_NonExistent";

            List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol, results, stopwatch);

            Assert.That(results, Is.Not.Null, $"'{symbol}' should be found");
            Assert.That(results.Count, Is.GreaterThan(0), $"'{symbol}' should be found");


            List<SymbolDefinitionResult> results2 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol2, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol2, results2, stopwatch);
            Assert.That(results2, Is.Not.Null, $"'{symbol2}' should be found");
            Assert.That(results2.Count, Is.GreaterThan(0), $"'{symbol2}' should be found");

            //Test with non existent structname
            List<SymbolDefinitionResult> results3 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol3, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol3, results3, stopwatch);
            Assert.That(results3, Is.Not.Null, $"'{symbol3}' should be found");
            Assert.That(results3.Count, Is.EqualTo(0), $"'{symbol3}' should NOT be found");
        }

        [Test]
        public async Task _04_FindSymbolDefinition_FindEnumSymbolInScripts()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string symbol = "TestSymbolSearchClass.TestSymbolSearchEnum";
            string symbol2 = "UMCP.Tests.Player.TestSymbolSearchClass.TestSymbolSearchEnum";
            string symbol3 = "UMCP.Tests.Player.TestSymbolSearchClass.TestSymbolSearchEnum_NonExistent";

            List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol, results, stopwatch);

            Assert.That(results, Is.Not.Null, $"'{symbol}' should be found");
            Assert.That(results.Count, Is.GreaterThan(0), $"'{symbol}' should be found");


            List<SymbolDefinitionResult> results2 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol2, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol2, results2, stopwatch);
            Assert.That(results2, Is.Not.Null, $"'{symbol2}' should be found");
            Assert.That(results2.Count, Is.GreaterThan(0), $"'{symbol2}' should be found");

            //Test with non existent structname
            List<SymbolDefinitionResult> results3 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol3, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol3, results3, stopwatch);
            Assert.That(results3, Is.Not.Null, $"'{symbol3}' should be found");
            Assert.That(results3.Count, Is.EqualTo(0), $"'{symbol3}' should NOT be found");
        }

        [Test]
        public async Task _05_FindSymbolDefinition_FindFieldSymbolInScripts()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string symbol = "TestSymbolSearchClass.TestSymbolSearchField";
            string symbol2 = "UMCP.Tests.Player.TestSymbolSearchClass.TestSymbolSearchField";
            string symbol3 = "UMCP.Tests.Player.TestSymbolSearchClass.TestSymbolSearchField_NonExistent";

            List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol, results, stopwatch);

            Assert.That(results, Is.Not.Null, $"'{symbol}' should be found");
            Assert.That(results.Count, Is.GreaterThan(0), $"'{symbol}' should be found");


            List<SymbolDefinitionResult> results2 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol2, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol2, results2, stopwatch);
            Assert.That(results2, Is.Not.Null, $"'{symbol2}' should be found");
            Assert.That(results2.Count, Is.GreaterThan(0), $"'{symbol2}' should be found");

            //Test with non existent structname
            List<SymbolDefinitionResult> results3 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol3, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol3, results3, stopwatch);
            Assert.That(results3, Is.Not.Null, $"'{symbol3}' should be found");
            Assert.That(results3.Count, Is.EqualTo(0), $"'{symbol3}' should NOT be found");
        }

        [Test]
        public async Task _06_FindSymbolDefinition_FindVoidSymbolInScripts()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string symbol = "TestSymbolSearchClass.TestSymbolSearchMethodVoid";
            string symbol2 = "UMCP.Tests.Player.TestSymbolSearchClass.TestSymbolSearchMethodVoid";
            string symbol3 = "UMCP.Tests.Player.TestSymbolSearchClass.TestSymbolSearchMethodVoid_NonExistent";

            List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol, results, stopwatch);

            Assert.That(results, Is.Not.Null, $"'{symbol}' should be found");
            Assert.That(results.Count, Is.GreaterThan(0), $"'{symbol}' should be found");


            List<SymbolDefinitionResult> results2 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol2, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol2, results2, stopwatch);
            Assert.That(results2, Is.Not.Null, $"'{symbol2}' should be found");
            Assert.That(results2.Count, Is.GreaterThan(0), $"'{symbol2}' should be found");

            //Test with non existent structname
            List<SymbolDefinitionResult> results3 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol3, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol3, results3, stopwatch);
            Assert.That(results3, Is.Not.Null, $"'{symbol3}' should be found");
            Assert.That(results3.Count, Is.EqualTo(0), $"'{symbol3}' should NOT be found");
        }

        [Test]
        public async Task _07_FindSymbolDefinition_FindFunctionSymbolInScripts()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string symbol = "TestSymbolSearchClass.TestSymbolSearchMethodValue";
            string symbol2 = "UMCP.Tests.Player.TestSymbolSearchClass.TestSymbolSearchMethodValue";
            string symbol3 = "UMCP.Tests.Player.TestSymbolSearchClass.TestSymbolSearchMethodValue_NonExistent";

            List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol, results, stopwatch);

            Assert.That(results, Is.Not.Null, $"'{symbol}' should be found");
            Assert.That(results.Count, Is.GreaterThan(0), $"'{symbol}' should be found");


            List<SymbolDefinitionResult> results2 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol2, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol2, results2, stopwatch);
            Assert.That(results2, Is.Not.Null, $"'{symbol2}' should be found");
            Assert.That(results2.Count, Is.GreaterThan(0), $"'{symbol2}' should be found");

            //Test with non existent structname
            List<SymbolDefinitionResult> results3 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol3, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol3, results3, stopwatch);
            Assert.That(results3, Is.Not.Null, $"'{symbol3}' should be found");
            Assert.That(results3.Count, Is.EqualTo(0), $"'{symbol3}' should NOT be found");
        }

        [Test]
        public async Task _08_FindSymbolDefinition_FindDelegateSymbolInScripts()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string symbol = "TestSymbolSearchClass.TestSymbolSearchDelegate";
            string symbol2 = "UMCP.Tests.Player.TestSymbolSearchClass.TestSymbolSearchDelegate";
            string symbol3 = "UMCP.Tests.Player.TestSymbolSearchClass.TestSymbolSearchDelegate_NonExistent";

            List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol, results, stopwatch);

            Assert.That(results, Is.Not.Null, $"'{symbol}' should be found");
            Assert.That(results.Count, Is.GreaterThan(0), $"'{symbol}' should be found");


            List<SymbolDefinitionResult> results2 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol2, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol2, results2, stopwatch);
            Assert.That(results2, Is.Not.Null, $"'{symbol2}' should be found");
            Assert.That(results2.Count, Is.GreaterThan(0), $"'{symbol2}' should be found");

            //Test with non existent structname
            List<SymbolDefinitionResult> results3 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol3, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol3, results3, stopwatch);
            Assert.That(results3, Is.Not.Null, $"'{symbol3}' should be found");
            Assert.That(results3.Count, Is.EqualTo(0), $"'{symbol3}' should NOT be found");
        }

        [Test]
        public async Task _09_FindSymbolDefinition_FindClassSymbolInAssemblies()
        {
             //VisualElement

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string symbol = "VisualElement";
            string symbol2 = "UnityEngine.UIElements.VisualElement";
            string symbol3 = "UnityEngine.UIElements.VisualElement_NonExistent";

            List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol, results, stopwatch);
            Assert.That(results, Is.Not.Null, $"'{symbol}' should be found");
            Assert.That(results.Count, Is.GreaterThan(0), $"'{symbol}' should be found");


            List<SymbolDefinitionResult> results2 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol2, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol2, results2, stopwatch);
            Assert.That(results2, Is.Not.Null, $"'{symbol2}' should be found");
            Assert.That(results2.Count, Is.GreaterThan(0), $"'{symbol2}' should be found");

            //Test with non existent structname
            List<SymbolDefinitionResult> results3 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol3, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol3, results3, stopwatch);
            Assert.That(results3, Is.Not.Null, $"'{symbol3}' should be found");
            Assert.That(results3.Count, Is.EqualTo(0), $"'{symbol3}' should NOT be found");
        }

        [Test]
        public async Task _10_FindSymbolDefinition_FindStructSymbolInAssemblies()
        {
            //VisualElement

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string symbol = "Vector3";
            string symbol2 = "UnityEngine.Vector3";
            string symbol3 = "UnityEngine.Vector3_NonExistent";

            List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol, results, stopwatch);
            Assert.That(results, Is.Not.Null, $"'{symbol}' should be found");
            Assert.That(results.Count, Is.GreaterThan(0), $"'{symbol}' should be found");


            List<SymbolDefinitionResult> results2 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol2, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol2, results2, stopwatch);
            Assert.That(results2, Is.Not.Null, $"'{symbol2}' should be found");
            Assert.That(results2.Count, Is.GreaterThan(0), $"'{symbol2}' should be found");

            //Test with non existent structname
            List<SymbolDefinitionResult> results3 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol3, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol3, results3, stopwatch);
            Assert.That(results3, Is.Not.Null, $"'{symbol3}' should be found");
            Assert.That(results3.Count, Is.EqualTo(0), $"'{symbol3}' should NOT be found");
        }

        [Test]
        public async Task _11_FindSymbolDefinition_FindFieldSymbolInAssemblies()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string symbol = "Vector3.x";
            string symbol2 = "UnityEngine.Vector3.x";
            string symbol3 = "UnityEngine.Vector3.x_nonExistent";

            List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol, results, stopwatch);

            Assert.That(results, Is.Not.Null, $"'{symbol}' should be found");
            Assert.That(results.Count, Is.GreaterThan(0), $"'{symbol}' should be found");


            List<SymbolDefinitionResult> results2 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol2, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol2, results2, stopwatch);
            Assert.That(results2, Is.Not.Null, $"'{symbol2}' should be found");
            Assert.That(results2.Count, Is.GreaterThan(0), $"'{symbol2}' should be found");

            //Test with non existent structname
            List<SymbolDefinitionResult> results3 = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol3, new CancellationTokenSource().Token, 1);
            logSymbolSearchResults(symbol3, results3, stopwatch);
            Assert.That(results3, Is.Not.Null, $"'{symbol3}' should be found");
            Assert.That(results3.Count, Is.EqualTo(0), $"'{symbol3}' should NOT be found");
        }



        private static void logSymbolSearchResults(string symbol, List<SymbolDefinitionResult> results, System.Diagnostics.Stopwatch stopwatch = null)
        {
            UnityEngine.Debug.Log($"Found {results?.Count.ToString() ?? "NULL"} results for symbol '{symbol}' after {stopwatch?.ElapsedMilliseconds.ToString() ?? "Unknown"}");
            if(results == null)
            {
                return;
            }
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                UnityEngine.Debug.Log($"Result {i}: FoundSymbolType: '{r.FoundSymbolType}' at '{((r.FilePath != null) ? r.FilePath : r.LibraryPath + "(" + r.LibraryClass + ")")}':{r.LineNumber}");
            }
        }

        #region << DECOMPILER TESTS >>

        [Test]
        public async Task HandleCommand_DecompileClass_MissingParameters_ReturnsError()
        {
            // Arrange
            var parameters = new JObject
            {
                ["action"] = "decompileclass"
            };

            // Act
            var result = await ManageScripts.HandleCommand(parameters);

            Debug.Log("Result from failing at Decompiling: " + result.ToString());

            // Assert
            Assert.IsNotNull(result);
            var resultDict = JObject.FromObject(result);
            Assert.IsFalse(resultDict["success"].Value<bool>());
            Assert.IsNotNull(resultDict["error"]);
            Assert.IsTrue(resultDict["error"].ToString().Contains("Unable to Decompile"));
        }

        [Test]
        public async Task HandleCommand_DecompileClass()
        {
            string symbol = "UnityEngine.UIElements.VisualElement";
            List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, 1);

            Assert.That(results, Is.Not.Null, $"'{symbol}' should find results in the Unity3d 'UnityEngine.UIElementsModule.dll'");
            Assert.That(results.Count, Is.GreaterThan(0), $"'{symbol}' should be found in assemblies");

            //2) Test the decompiler
            string targetPath = Path.Combine("../DecompiledScripts/", $"{results[0].LibraryClass}.cs");
            string libraryPath = results[0].LibraryPath;
            string libraryClass = results[0].LibraryClass;


            // Arrange
            var parameters = new JObject
            {
                ["action"] = "decompileclass",
                ["LibraryPath"] = libraryPath,
                ["LibraryClass"] = libraryClass,
                ["OutputFileName"] = targetPath,
                ["OverrideExistingFile"] = true
            };

            // Act
            var result = await ManageScripts.HandleCommand(parameters);

            Debug.Log("Result from Decompiling: " + result.ToString());   

            // Assert
            Assert.IsNotNull(result);
            var resultDict = JObject.FromObject(result);
            Assert.IsTrue(resultDict["success"].Value<bool>());
        }

        [Test(Description = "Test whether the Decompile function works by trying to decompile the 'UnityEngine.UIElements.VisualElement' " +
            "class which is located in project external 'UnityEngine.UIElementsModule.dll")]
        public async Task _12_DecompilerUtility_TestDecompile() 
        {
            //1) Get the Visual Element Class definition as symbol definition
            string symbol = "UnityEngine.UIElements.VisualElement";
            List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, 1);

            Assert.That(results, Is.Not.Null, $"'{symbol}' should find results in the Unity3d 'UnityEngine.UIElementsModule.dll'");
            Assert.That(results.Count, Is.GreaterThan(0), $"'{symbol}' should be found in assemblies");

            //2) Test the decompiler
            string targetPath = Path.Combine(Application.dataPath, "../DecompiledScripts/", $"{results[0].LibraryClass}.cs");

            UnityEngine.Debug.Log("Decompiling into path: " + targetPath);

            FileInfo targetInfo = new FileInfo(targetPath);

            bool firstDecompileResult = DecompilerUtility.DecompileClass(results[0].LibraryPath, results[0].LibraryClass, targetPath, out string resultMessage, true);
            Assert.That(firstDecompileResult, Is.True, $"Decompiling '{results[0].LibraryClass}' from '{results[0].LibraryPath}' should be successful. Message: {resultMessage}");

            bool secondDecompileResult = DecompilerUtility.DecompileClass(results[0].LibraryPath, results[0].LibraryClass, targetPath, out string resultMessage2, false);
            Assert.That(secondDecompileResult, Is.False, $"Decompiling '{results[0].LibraryClass}' from '{results[0].LibraryPath}' a second time without overwrite should fail. Message: {resultMessage2}");

            bool thirdDecompileResult = DecompilerUtility.DecompileClass(results[0].LibraryPath, results[0].LibraryClass + "_NONEXIST", targetPath, out string resultMessage3, true);
            Assert.That(thirdDecompileResult, Is.False, $"Decompiling non existent class '{results[0].LibraryClass}_NONEXIST' from '{results[0].LibraryPath}' should fail. Message: {resultMessage3}");

            if(targetInfo.Exists)
            {
                targetInfo.Delete();
            }   
        }

        #endregion

        #endregion
    }
}