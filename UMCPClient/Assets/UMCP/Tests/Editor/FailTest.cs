using NUnit.Framework;
using UnityEngine;

namespace UMCP.Tests.Editor
{
    public class FailTest 
    {
        [Test]
        public void FailTestFunction()
        {
            Assert.Fail("This test is designed to fail.");
        }
    }
}
