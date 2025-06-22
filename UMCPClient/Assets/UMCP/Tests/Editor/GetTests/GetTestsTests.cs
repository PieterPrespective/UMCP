using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UMCP.Editor.Tools;

namespace UMCP.Tests.Editor.GetTests
{
    /// <summary>
    /// Unit tests for GetTests tool data structures and utilities
    /// </summary>
    [TestFixture]
    public class GetTestsTests
    {
        /// <summary>
        /// Tests that TestInfo struct properly stores and retrieves data
        /// </summary>
        [Test]
        public void TestInfo_ShouldStoreAndRetrieveData()
        {
            var testInfo = new TestInfo
            {
                TestName = "MyNamespace.MyClass.MyTestMethod",
                TestAssembly = "MyAssembly",
                TestNamespace = "MyNamespace",
                ContainerScript = "MyClass"
            };
            
            Assert.AreEqual("MyNamespace.MyClass.MyTestMethod", testInfo.TestName);
            Assert.AreEqual("MyAssembly", testInfo.TestAssembly);
            Assert.AreEqual("MyNamespace", testInfo.TestNamespace);
            Assert.AreEqual("MyClass", testInfo.ContainerScript);
        }
        
        /// <summary>
        /// Tests that GetTestsParameters struct properly stores test mode and filter
        /// </summary>
        [Test]
        public void GetTestsParameters_ShouldStoreParameters()
        {
            var parameters = new GetTestsParameters
            {
                TestMode = "EditMode",
                Filter = "MyTestFilter"
            };
            
            Assert.AreEqual("EditMode", parameters.TestMode);
            Assert.AreEqual("MyTestFilter", parameters.Filter);
        }
        
        /// <summary>
        /// Tests validation of test mode values
        /// </summary>
        [TestCase("EditMode", true)]
        [TestCase("PlayMode", true)]
        [TestCase("All", true)]
        [TestCase("InvalidMode", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void TestMode_ValidationShouldWork(string testMode, bool expectedValid)
        {
            bool isValid = testMode == "EditMode" || testMode == "PlayMode" || testMode == "All";
            Assert.AreEqual(expectedValid, isValid);
        }
        
        /// <summary>
        /// Tests namespace extraction logic
        /// </summary>
        [TestCase("Namespace.SubNamespace.Class.Method", "Namespace.SubNamespace")]
        [TestCase("Namespace.Class.Method", "Namespace")]
        [TestCase("Class.Method", "")]
        [TestCase("Method", "")]
        public void ExtractNamespace_ShouldWorkCorrectly(string fullName, string expectedNamespace)
        {
            // Replicate the extraction logic from GetTestsUtility
            var lastDotIndex = fullName.LastIndexOf('.');
            string actualNamespace = "";
            
            if (lastDotIndex > 0)
            {
                var nameWithoutMethod = fullName.Substring(0, lastDotIndex);
                var secondLastDotIndex = nameWithoutMethod.LastIndexOf('.');
                if (secondLastDotIndex > 0)
                {
                    actualNamespace = nameWithoutMethod.Substring(0, secondLastDotIndex);
                }
            }
            
            Assert.AreEqual(expectedNamespace, actualNamespace);
        }
        
        /// <summary>
        /// Tests container script extraction logic
        /// </summary>
        [TestCase("Namespace.SubNamespace.Class.Method", "Class")]
        [TestCase("Namespace.Class.Method", "Class")]
        [TestCase("Class.Method", "Class")]
        [TestCase("Method", "")]
        public void ExtractContainerScript_ShouldWorkCorrectly(string fullName, string expectedScript)
        {
            // Replicate the extraction logic from GetTestsUtility
            var parts = fullName.Split('.');
            string actualScript = "";
            
            if (parts.Length >= 2)
            {
                actualScript = parts[parts.Length - 2];
            }
            
            Assert.AreEqual(expectedScript, actualScript);
        }
        
        /// <summary>
        /// Tests filter matching logic
        /// </summary>
        [TestCase("MyNamespace.MyClass.MyTest", "MyClass", true)]
        [TestCase("MyNamespace.MyClass.MyTest", "myclass", true)] // Case insensitive
        [TestCase("MyNamespace.MyClass.MyTest", "MyNamespace", true)]
        [TestCase("MyNamespace.MyClass.MyTest", "MyTest", true)]
        [TestCase("MyNamespace.MyClass.MyTest", "NotFound", false)]
        [TestCase("MyNamespace.MyClass.MyTest", "", true)] // Empty filter matches all
        [TestCase("MyNamespace.MyClass.MyTest", null, true)] // Null filter matches all
        public void FilterMatching_ShouldWorkCorrectly(string testName, string filter, bool expectedMatch)
        {
            var testInfo = new TestInfo
            {
                TestName = testName,
                TestNamespace = "MyNamespace"
            };
            
            bool actualMatch = string.IsNullOrEmpty(filter) || 
                testInfo.TestName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                testInfo.TestNamespace.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
            
            Assert.AreEqual(expectedMatch, actualMatch);
        }
    }
}