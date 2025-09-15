using UnityEngine;

namespace UMCP.Tests.Player
{
    /// <summary>
    /// Class used for testing the UMCP symbol search function
    /// </summary>
    public class TestSymbolSearchClass
    {
        public delegate int TestSymbolSearchDelegate(int x, int y);

        public double TestSymbolSearchField = 3.14;

        public enum TestSymbolSearchEnum
        {
            FirstValue,
            SecondValue,
            ThirdValue
        }
        public int TestSymbolSearchProperty { 
            get { return 42; }
            set { Debug.Log("Property set to " + value); }
        }

        public void TestSymbolSearchMethodVoid()
        {
            Debug.Log("TestSymbolSearchMethod executed");
        }

        public int TestSymbolSearchMethodValue()
        {
            return 12;
        }
    }

    /// <summary>
    /// Struct used for testing the UMCP symbol search function
    /// </summary>
    public struct TestSymbolSearchStruct
    {
        public int StructField;
        public void StructMethod()
        {
            Debug.Log("StructMethod executed");
        }
    }
}
