using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace UMCP.Tests.Integration.ExcludedNamespace
{
    /// <summary>
    /// Tests in this namespace should be excluded when testing namespace filtering.
    /// This is used to validate the excluded namespace functionality of the RunTests tool.
    /// </summary>
    [TestFixture]
    public class ExcludedNamespaceTest
    {
        /// <summary>
        /// Test that should be excluded when the namespace is filtered out
        /// </summary>
        [Test]
        public void TestThatShouldBeExcluded()
        {
            Assert.That(1 + 1, Is.EqualTo(2), "This test should be excluded when namespace filtering is applied");
        }

        /// <summary>
        /// Another test that should be excluded when the namespace is filtered out
        /// </summary>
        [Test]
        public void AnotherTestThatShouldBeExcluded()
        {
            Assert.That(10 / 2, Is.EqualTo(5), "This test should also be excluded when namespace filtering is applied");
        }

        /// <summary>
        /// Unity test that should be excluded
        /// </summary>
        [UnityTest]
        public IEnumerator UnityTestThatShouldBeExcluded()
        {
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(true, Is.True, "This Unity test should be excluded when namespace filtering is applied");
        }
    }
}