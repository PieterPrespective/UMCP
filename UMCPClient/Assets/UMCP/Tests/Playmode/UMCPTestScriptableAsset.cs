using UnityEngine;

namespace UMCP.Tests.Player
{
    /// <summary>
    /// Simple ScriptableObject for testing asset creation.
    /// </summary>
    [System.Serializable]
    [CreateAssetMenu(fileName = "UMCPTestScriptableObject", menuName = "UMCP/Testing/TestScriptableAsset")]
    public class UMCPTestScriptableAsset : ScriptableObject
    {
        public string testString = "Test Value";
        public int testInt = 42;
        public float testFloat = 3.14f;
    }
}
