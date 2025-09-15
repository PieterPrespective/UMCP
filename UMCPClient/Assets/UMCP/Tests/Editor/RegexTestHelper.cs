using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UMCP.Tests.Editor
{
    /// <summary>
    /// Helper class to test regex patterns for symbol detection.
    /// </summary>
    public static class RegexTestHelper
    {
        /// <summary>
        /// Tests the pattern used for matching StateStorage property definition
        /// </summary>
        public static void TestStateStoragePropertyPattern()
        {
            string testLine = "        private static IntegrationTestStateStorage StateStorage";
            string symbolName = "StateStorage";
            
            Debug.Log($"[RegexTestHelper] Testing StateStorage property detection");
            Debug.Log($"[RegexTestHelper] Target line: '{testLine}'");
            
            // Create the same patterns used in SymbolSearchUtility for properties
            var patterns = new List<Regex>();
            string name = Regex.Escape(symbolName);
            
            // Property definition patterns - handle both single-line and multi-line formats
            // Note: Using [\w<>\[\].]+ to handle type names with dots (namespaces)
            // Single line: private static Type PropertyName { get; set; }
            patterns.Add(new Regex($@"^\s*(?:public\s+|private\s+|protected\s+|internal\s+|static\s+)+[\w<>\[\].]+\s+{name}\s*\{{\s*(?:get|set)", RegexOptions.None));
            // Multi-line: private static Type PropertyName\n{\n    get
            patterns.Add(new Regex($@"^\s*(?:public\s+|private\s+|protected\s+|internal\s+|static\s+)+[\w<>\[\].]+\s+{name}\s*$", RegexOptions.None));
            // Also check for properties without access modifiers (default private in C#)
            patterns.Add(new Regex($@"^\s*(?:static\s+)?[\w<>\[\].]+\s+{name}\s*\{{\s*(?:get|set)", RegexOptions.None));
            patterns.Add(new Regex($@"^\s*(?:static\s+)?[\w<>\[\].]+\s+{name}\s*$", RegexOptions.None));
            
            for (int i = 0; i < patterns.Count; i++)
            {
                var pattern = patterns[i];
                bool matches = pattern.IsMatch(testLine);
                Debug.Log($"[RegexTestHelper] Pattern {i + 1}: {pattern} -> Match: {matches}");
                
                if (matches)
                {
                    var match = pattern.Match(testLine);
                    Debug.Log($"[RegexTestHelper] Match groups: {match.Groups.Count}");
                    for (int j = 0; j < match.Groups.Count; j++)
                    {
                        Debug.Log($"[RegexTestHelper] Group {j}: '{match.Groups[j].Value}'");
                    }
                }
            }
        }

        public static void TestManageIntegrationTestsPattern()
        {
            string testLine = "    public static class ManageIntegrationTests";
            string symbolName = "ManageIntegrationTests";
            
            // Create the same pattern used in SymbolSearchUtility
            var pattern = new Regex($@"^\s*(?:\b(?:public|private|protected|internal|static|abstract|sealed|partial)\s+)*(class|struct|interface|enum)\s+{Regex.Escape(symbolName)}\b", RegexOptions.None);
            
            Debug.Log($"[RegexTestHelper] Testing line: '{testLine}'");
            Debug.Log($"[RegexTestHelper] Pattern: {pattern}");
            Debug.Log($"[RegexTestHelper] Match result: {pattern.IsMatch(testLine)}");
            
            if (pattern.IsMatch(testLine))
            {
                var match = pattern.Match(testLine);
                Debug.Log($"[RegexTestHelper] Match groups: {match.Groups.Count}");
                for (int i = 0; i < match.Groups.Count; i++)
                {
                    Debug.Log($"[RegexTestHelper] Group {i}: '{match.Groups[i].Value}'");
                }
            }
            
            // Test other variations
            string[] testLines = {
                "public static class ManageIntegrationTests",
                "    public static class ManageIntegrationTests",
                "public class ManageIntegrationTests",
                "static class ManageIntegrationTests",
                "    static class ManageIntegrationTests {",
                "public static class ManageIntegrationTests {"
            };
            
            foreach (var line in testLines)
            {
                Debug.Log($"[RegexTestHelper] Line: '{line}' -> Match: {pattern.IsMatch(line)}");
            }
        }

        public static void TestHandleCommandMethodPattern()
        {
            string testLine = "        public static object HandleCommand(JObject parameters)";
            string symbolName = "HandleCommand";
            
            Debug.Log($"[RegexTestHelper] Testing HandleCommand method detection");
            Debug.Log($"[RegexTestHelper] Target line: '{testLine}'");
            
            // Create the same pattern used in SymbolSearchUtility for methods
            string name = Regex.Escape(symbolName);
            var pattern = new Regex($@"^\s*(?:public\s+|private\s+|protected\s+|internal\s+|static\s+|virtual\s+|override\s+|abstract\s+|async\s+)*[\w<>\[\]]+\s+{name}\s*\(", RegexOptions.None);
            
            bool matches = pattern.IsMatch(testLine);
            Debug.Log($"[RegexTestHelper] Pattern: {pattern}");
            Debug.Log($"[RegexTestHelper] Match result: {matches}");
            
            if (matches)
            {
                var match = pattern.Match(testLine);
                Debug.Log($"[RegexTestHelper] Match groups: {match.Groups.Count}");
                for (int i = 0; i < match.Groups.Count; i++)
                {
                    Debug.Log($"[RegexTestHelper] Group {i}: '{match.Groups[i].Value}'");
                }
            }
            
            // Test variations
            string[] testLines = {
                "public static object HandleCommand(JObject parameters)",
                "        public static object HandleCommand(JObject parameters)",
                "public object HandleCommand(JObject parameters)",
                "static object HandleCommand(JObject parameters)",
                "        public static object HandleCommand("
            };
            
            foreach (var line in testLines)
            {
                Debug.Log($"[RegexTestHelper] Line: '{line}' -> Match: {pattern.IsMatch(line)}");
            }
        }
    }
}