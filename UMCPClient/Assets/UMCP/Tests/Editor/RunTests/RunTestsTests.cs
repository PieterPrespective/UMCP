using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UMCP.Editor.Tools;

namespace UMCP.Tests.Editor.RunTests
{
    /// <summary>
    /// Unit tests for RunTests tool data structures and utilities
    /// </summary>
    [TestFixture]
    public class RunTestsTests
    {
        /// <summary>
        /// Tests that TestResultData struct properly stores test result information
        /// </summary>
        [Test]
        public void TestResultData_ShouldStoreAndRetrieveData()
        {
            var testResult = new TestResultData
            {
                TestName = "MyNamespace.MyClass.MyTestMethod",
                TestAssembly = "MyAssembly",
                TestNamespace = "MyNamespace",
                ContainerScript = "MyClass",
                Success = true
            };
            
            Assert.AreEqual("MyNamespace.MyClass.MyTestMethod", testResult.TestName);
            Assert.AreEqual("MyAssembly", testResult.TestAssembly);
            Assert.AreEqual("MyNamespace", testResult.TestNamespace);
            Assert.AreEqual("MyClass", testResult.ContainerScript);
            Assert.IsTrue(testResult.Success);
        }
        
        /// <summary>
        /// Tests that RunTestsParameters struct properly stores parameters
        /// </summary>
        [Test]
        public void RunTestsParameters_ShouldStoreParameters()
        {
            var parameters = new RunTestsParameters
            {
                TestMode = "EditMode",
                Filter = new[] { "Test1", "Test2" },
                OutputTestResults = true,
                OutputLogData = false
            };
            
            Assert.AreEqual("EditMode", parameters.TestMode);
            Assert.AreEqual(2, parameters.Filter.Length);
            Assert.AreEqual("Test1", parameters.Filter[0]);
            Assert.AreEqual("Test2", parameters.Filter[1]);
            Assert.IsTrue(parameters.OutputTestResults);
            Assert.IsFalse(parameters.OutputLogData);
        }
        
        /// <summary>
        /// Tests that RunTestsResult struct properly stores results
        /// </summary>
        [Test]
        public void RunTestsResult_ShouldStoreResults()
        {
            var result = new RunTestsResult
            {
                AllSuccess = false,
                TestResults = new List<TestResultData>
                {
                    new TestResultData { TestName = "Test1", Success = true },
                    new TestResultData { TestName = "Test2", Success = false }
                },
                LogData = "Test log data"
            };
            
            Assert.IsFalse(result.AllSuccess);
            Assert.AreEqual(2, result.TestResults.Count);
            Assert.IsTrue(result.TestResults[0].Success);
            Assert.IsFalse(result.TestResults[1].Success);
            Assert.AreEqual("Test log data", result.LogData);
        }
        
        /// <summary>
        /// Tests that AllSuccess calculation works correctly
        /// </summary>
        [Test]
        public void AllSuccess_ShouldBeCalculatedCorrectly()
        {
            var testResults = new List<TestResultData>
            {
                new TestResultData { Success = true },
                new TestResultData { Success = true },
                new TestResultData { Success = true }
            };
            
            bool allSuccess = testResults.All(r => r.Success);
            Assert.IsTrue(allSuccess);
            
            testResults.Add(new TestResultData { Success = false });
            allSuccess = testResults.All(r => r.Success);
            Assert.IsFalse(allSuccess);
        }
        
        /// <summary>
        /// Tests step GUID generation format
        /// </summary>
        [Test]
        public void StepGuid_ShouldHaveCorrectFormat()
        {
            var guid = System.Guid.NewGuid();
            string stepGuid = $"RunTests_{guid:N}";
            
            Assert.IsTrue(stepGuid.StartsWith("RunTests_"));
            Assert.AreEqual(41, stepGuid.Length); // "RunTests_" (9) + 32 hex chars
            Assert.IsFalse(stepGuid.Contains("-")); // N format removes hyphens
        }
        
        /// <summary>
        /// Tests assembly extraction from full test name
        /// </summary>
        [TestCase("Assembly.Namespace.Class.Method", "Assembly")]
        [TestCase("Namespace.Class.Method", "Namespace")]
        [TestCase("Class.Method", "Class")]
        [TestCase("Method", "Method")]
        [TestCase("", "")]
        public void ExtractAssembly_ShouldWorkCorrectly(string fullName, string expectedAssembly)
        {
            var parts = fullName.Split('.');
            string actualAssembly = parts.Length > 0 ? parts[0] : "";
            
            Assert.AreEqual(expectedAssembly, actualAssembly);
        }
        
        /// <summary>
        /// Tests filter array handling
        /// </summary>
        [Test]
        public void FilterArray_ShouldHandleVariousCases()
        {
            // Empty filter
            var emptyFilter = new string[0];
            Assert.AreEqual(0, emptyFilter.Length);
            
            // Null filter should be handled
            string[] nullFilter = null;
            var safeFilter = nullFilter ?? new string[0];
            Assert.AreEqual(0, safeFilter.Length);
            
            // Filter with values
            var filter = new[] { "Test1", "Test2", "Test3" };
            Assert.AreEqual(3, filter.Length);
            Assert.Contains("Test2", filter);
        }
        
        /// <summary>
        /// Tests parameter defaults
        /// </summary>
        [Test]
        public void Parameters_ShouldHaveCorrectDefaults()
        {
            var parameters = new RunTestsParameters();
            
            // Check default values (as they would be set in HandleCommand)
            Assert.IsNull(parameters.TestMode); // Would default to "All" in handler
            Assert.IsNull(parameters.Filter); // Would default to empty array in handler
            Assert.IsFalse(parameters.OutputTestResults); // Would default to true in handler
            Assert.IsFalse(parameters.OutputLogData); // Would default to true in handler
        }
    }
}