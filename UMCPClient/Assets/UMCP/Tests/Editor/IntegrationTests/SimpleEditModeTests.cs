using NUnit.Framework;

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
        public void TestAddition()
        {
            Assert.That(1 + 1, Is.EqualTo(2));
        }
        
        /// <summary>
        /// Test that validates basic subtraction - should pass
        /// </summary>
        [Test]
        public void TestSubstraction()
        {
            Assert.That(5 - 1, Is.EqualTo(4));
        }
        
        /// <summary>
        /// Test that contains a faulty assertion - should fail
        /// </summary>
        [Test]
        public void TestFaulty()
        {
            Assert.That(6 + 2, Is.EqualTo(7), "This test is intentionally faulty");
        }
    }
}