using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UMCP.Editor.Tools.ManageScripts
{
    /// <summary>
    /// Utility class for searching through script files in the Unity project.
    /// </summary>
    public static class ScriptSearchUtility
    {
        /// <summary>
        /// Result of a script file search operation.
        /// </summary>
        public class SearchResult
        {
            public string FilePath { get; set; }
            public int LineNumber { get; set; }
            public string LineContent { get; set; }
        }

        /// <summary>
        /// Searches through all script files in the project for the specified pattern.
        /// </summary>
        /// <param name="searchFilter">The text or pattern to search for</param>
        /// <param name="matchCase">Whether to match case</param>
        /// <param name="matchWholeWord">Whether to match whole words only</param>
        /// <param name="useRegularExpression">Whether to interpret searchFilter as regex</param>
        /// <returns>List of search results</returns>
        public static List<SearchResult> SearchInScriptFiles(
            string searchFilter, 
            bool matchCase = false, 
            bool matchWholeWord = false, 
            bool useRegularExpression = false)
        {
            //var results = new List<SearchResult>();

            var results = new ConcurrentBag<SearchResult>();

            if (string.IsNullOrEmpty(searchFilter))
            {
                return results.ToList();
            }

            try
            {
                // Get all script files from Assets folder
                var scriptFiles = GetAllScriptFiles();
                
                // Prepare the search pattern
                Regex searchRegex = CreateSearchRegex(searchFilter, matchCase, matchWholeWord, useRegularExpression);

                // Search each file in parallel
                Parallel.ForEach(scriptFiles, scriptFile =>
                {
                    SearchInFile(scriptFile, searchRegex, results);
                });
                
                Debug.Log($"[ScriptSearchUtility] Found {results.Count} matches in {scriptFiles.Count} files");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScriptSearchUtility] Error during search: {e.Message}");
            }
            
            return results.ToList();
        }

        /// <summary>
        /// Gets all script files in the project including Assets and Packages folders.
        /// </summary>
        internal static List<string> GetAllScriptFiles()
        {
            var scriptFiles = new List<string>();
            
            // Search in Assets folder
            string assetsPath = Application.dataPath;
            if (Directory.Exists(assetsPath))
            {
                var assetScripts = Directory.GetFiles(assetsPath, "*.cs", SearchOption.AllDirectories)
                    .Select(path => GetRelativePathFromAssets(path))
                    .ToList();
                scriptFiles.AddRange(assetScripts);
            }
            
            // Search in Library/PackageCache for package scripts
            string packageCachePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "PackageCache");
            if (Directory.Exists(packageCachePath))
            {
                try
                {
                    var packageScripts = Directory.GetFiles(packageCachePath, "*.cs", SearchOption.AllDirectories)
                        .Where(path => !path.Contains("Tests") && !path.Contains("TestAssemblies")) // Filter out test files
                        .Select(path => GetRelativePathFromProject(path))
                        .ToList();
                    scriptFiles.AddRange(packageScripts);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ScriptSearchUtility] Could not search package cache: {e.Message}");
                }
            }
            
            return scriptFiles;
        }

        /// <summary>
        /// Creates a regex pattern based on search parameters.
        /// </summary>
        internal static Regex CreateSearchRegex(string searchFilter, bool matchCase, bool matchWholeWord, bool useRegularExpression)
        {
            string pattern;
            
            if (useRegularExpression)
            {
                pattern = searchFilter;
            }
            else
            {
                // Escape special regex characters if not using regex
                pattern = Regex.Escape(searchFilter);
                
                if (matchWholeWord)
                {
                    // Add word boundaries
                    pattern = $@"\b{pattern}\b";
                }
            }
            
            RegexOptions options = RegexOptions.None;
            if (!matchCase)
            {
                options |= RegexOptions.IgnoreCase;
            }
            
            return new Regex(pattern, options);
        }

        /// <summary>
        /// Searches for matches in a single file.
        /// </summary>
        private static void SearchInFile(string filePath, Regex searchRegex, ConcurrentBag<SearchResult> results)// List<SearchResult> results)
        {
            try
            {
                // Convert relative path to absolute path for file reading
                string absolutePath = GetAbsolutePathFromRelative(filePath);
                
                if (!File.Exists(absolutePath))
                {
                    return;
                }
                
                string[] lines = File.ReadAllLines(absolutePath);
                
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (searchRegex.IsMatch(line))
                    {
                        results.Add(new SearchResult
                        {
                            FilePath = filePath,
                            LineNumber = i + 1, // Line numbers are 1-based
                            LineContent = line.Trim()
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ScriptSearchUtility] Error reading file {filePath}: {e.Message}");
            }
        }

        /// <summary>
        /// Converts an absolute path to a relative path from the Assets folder.
        /// </summary>
        private static string GetRelativePathFromAssets(string absolutePath)
        {
            string assetsPath = Application.dataPath;
            if (absolutePath.StartsWith(assetsPath))
            {
                string relativePath = "Assets" + absolutePath.Substring(assetsPath.Length);
                return relativePath.Replace('\\', '/');
            }
            return absolutePath.Replace('\\', '/');
        }

        /// <summary>
        /// Converts an absolute path to a relative path from the project root.
        /// </summary>
        private static string GetRelativePathFromProject(string absolutePath)
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            if (absolutePath.StartsWith(projectPath))
            {
                string relativePath = absolutePath.Substring(projectPath.Length + 1);
                return relativePath.Replace('\\', '/');
            }
            return absolutePath.Replace('\\', '/');
        }

        /// <summary>
        /// Converts a relative path to an absolute path.
        /// </summary>
        private static string GetAbsolutePathFromRelative(string relativePath)
        {
            if (relativePath.StartsWith("Assets/"))
            {
                string assetsPath = Application.dataPath;
                return Path.Combine(assetsPath, relativePath.Substring(7)).Replace('\\', '/');
            }
            else if (relativePath.StartsWith("Library/PackageCache/"))
            {
                string projectPath = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(projectPath, relativePath).Replace('\\', '/');
            }
            else
            {
                // Assume it's relative to project root
                string projectPath = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(projectPath, relativePath).Replace('\\', '/');
            }
        }
    }
}