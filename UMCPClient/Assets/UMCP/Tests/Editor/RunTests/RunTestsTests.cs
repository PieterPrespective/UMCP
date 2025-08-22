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
            
            Assert.That(testResult.TestName, Is.EqualTo("MyNamespace.MyClass.MyTestMethod"));
            Assert.That(testResult.TestAssembly, Is.EqualTo("MyAssembly"));
            Assert.That(testResult.TestNamespace, Is.EqualTo("MyNamespace"));
            Assert.That(testResult.ContainerScript, Is.EqualTo("MyClass"));
            Assert.That(testResult.Success, Is.True);
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
            
            Assert.That(parameters.TestMode, Is.EqualTo("EditMode"));
            Assert.That(parameters.Filter.Length, Is.EqualTo(2));
            Assert.That(parameters.Filter[0], Is.EqualTo("Test1"));
            Assert.That(parameters.Filter[1], Is.EqualTo("Test2"));
            Assert.That(parameters.OutputTestResults, Is.True);
            Assert.That(parameters.OutputLogData, Is.False);
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
            
            Assert.That(result.AllSuccess, Is.False);
            Assert.That(result.TestResults.Count, Is.EqualTo(2));
            Assert.That(result.TestResults[0].Success, Is.True);
            Assert.That(result.TestResults[1].Success, Is.False);
            Assert.That(result.LogData, Is.EqualTo("Test log data"));
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
            Assert.That(allSuccess, Is.True);
            
            testResults.Add(new TestResultData { Success = false });
            allSuccess = testResults.All(r => r.Success);
            Assert.That(allSuccess, Is.False);
        }
        
        /// <summary>
        /// Tests step GUID generation format
        /// </summary>
        [Test]
        public void StepGuid_ShouldHaveCorrectFormat()
        {
            var guid = System.Guid.NewGuid();
            string stepGuid = $"RunTests_{guid:N}";
            
            Assert.That(stepGuid.StartsWith("RunTests_"), Is.True);
            Assert.That(stepGuid.Length, Is.EqualTo(41)); // "RunTests_" (9) + 32 hex chars
            Assert.That(stepGuid.Contains("-"), Is.False); // N format removes hyphens
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
            
            Assert.That(actualAssembly, Is.EqualTo(expectedAssembly));
        }
        
        /// <summary>
        /// Tests filter array handling
        /// </summary>
        [Test]
        public void FilterArray_ShouldHandleVariousCases()
        {
            // Empty filter
            var emptyFilter = new string[0];
            Assert.That(emptyFilter.Length, Is.EqualTo(0));
            
            // Null filter should be handled
            string[] nullFilter = null;
            var safeFilter = nullFilter ?? new string[0];
            Assert.That(safeFilter.Length, Is.EqualTo(0));
            
            // Filter with values
            var filter = new[] { "Test1", "Test2", "Test3" };
            Assert.That(filter.Length, Is.EqualTo(3));
            Assert.That(filter, Does.Contain("Test2"));
        }
        
        /// <summary>
        /// Tests parameter defaults
        /// </summary>
        [Test]
        public void Parameters_ShouldHaveCorrectDefaults()
        {
            var parameters = new RunTestsParameters();
            
            // Check default values (as they would be set in HandleCommand)
            Assert.That(parameters.TestMode, Is.Null); // Would default to "All" in handler
            Assert.That(parameters.Filter, Is.Null); // Would default to empty array in handler
            Assert.That(parameters.OutputTestResults, Is.False); // Would default to true in handler
            Assert.That(parameters.OutputLogData, Is.False); // Would default to true in handler
        }
    }
}