
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEditor.SearchService;
using UnityEngine;
using static UMCP.Editor.Tools.ManageScripts.SymbolSearchUtility;

namespace UMCP.Editor
{

    public class CompleteSolutionLoader
    {
        private readonly int MAX_PARALLELISM = -1;
        private readonly AdhocWorkspace _workspace;
        private readonly ConcurrentDictionary<string, (ProjectId, ProjectInfo)> _projectIdMap;

        public CompleteSolutionLoader()
        {

            _workspace = new AdhocWorkspace();
            _projectIdMap = new ConcurrentDictionary<string, (ProjectId, ProjectInfo)>();
            MAX_PARALLELISM = Math.Max(1, Environment.ProcessorCount - 1);
        }

        public async Task<Solution> LoadSolutionAsync(string solutionPath)
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Create base solution
            var solutionInfo = SolutionInfo.Create(
                SolutionId.CreateNewId(),
                VersionStamp.Create(),
                solutionPath);

            UnityEngine.Debug.Log($"[CompleteSolutionLoader] Created Solution info at {solutionPath} after {stopwatch.ElapsedMilliseconds} ms");

            var solution = _workspace.AddSolution(solutionInfo);

            // Parse .sln file to get project paths
            var projectPaths = ExtractProjectPaths(solutionPath);

            UnityEngine.Debug.Log($"[CompleteSolutionLoader] Identified {projectPaths.Count} projects after {stopwatch.ElapsedMilliseconds} ms");


            Parallel.ForEach(projectPaths, new ParallelOptions { MaxDegreeOfParallelism = MAX_PARALLELISM }, projectPath =>
            {
                loadProjectInfo(projectPath, _projectIdMap);
            });

            foreach(string projectPath in _projectIdMap.Keys)
            {
                solution = solution.AddProject(_projectIdMap[projectPath].Item2);
            }
            UnityEngine.Debug.Log($"[CompleteSolutionLoader] Created {projectPaths.Count} project maps after {stopwatch.ElapsedMilliseconds} ms");

            int parPerProject = Mathf.FloorToInt(MAX_PARALLELISM / 2);
            object solutionLock = new object();

            Parallel.ForEach(projectPaths, new ParallelOptions { MaxDegreeOfParallelism = 2 }, (projectPath, parState, idx) =>
            {
                populateProjectParallel(solution, solutionLock, projectPath, _projectIdMap, parPerProject);

                if (idx >= 0)
                {
                    parState.Break();
                }
            });

            UnityEngine.Debug.Log($"[CompleteSolutionLoader] Loadled {projectPaths.Count} projects after {stopwatch.ElapsedMilliseconds} ms");



            //// First pass: Create all projects (needed for project references)
            //foreach (var projectPath in projectPaths)
            //{
            //    var projectInfo = await CreateBasicProjectInfo(projectPath);
            //    solution = solution.AddProject(projectInfo);
            //    _projectIdMap[projectPath] = projectInfo.Id;
            //}

            //UnityEngine.Debug.Log($"[CompleteSolutionLoader] Created {projectPaths.Count} project maps after {stopwatch.ElapsedMilliseconds} ms");

            // Second pass: Add documents and references
            //foreach (var projectPath in projectPaths)
            //{
            //    solution = await PopulateProject(solution, projectPath);
            //}

            return solution;
        }

        private async void loadProjectInfo(string projectPath, ConcurrentDictionary<string, (ProjectId, ProjectInfo)> _projectIDMap)
        {
            var projectInfo = await CreateBasicProjectInfo(projectPath);
            _projectIDMap.TryAdd(projectPath, (projectInfo.Id, projectInfo));
        }


        private List<string> ExtractProjectPaths(string solutionPath)
        {
            var projectPaths = new List<string>();
            var solutionDir = Path.GetDirectoryName(solutionPath);
            var solutionContent = File.ReadAllText(solutionPath);

            var projectRegex = new Regex(@"Project\(""\{[^}]+\}""\)\s*=\s*""[^""]+"",\s*""([^""]+)"",");

            foreach (Match match in projectRegex.Matches(solutionContent))
            {
                var relativePath = match.Groups[1].Value;
                if (relativePath.EndsWith(".csproj"))
                {
                    var fullPath = Path.GetFullPath(Path.Combine(solutionDir, relativePath));
                    if (File.Exists(fullPath))
                    {
                        projectPaths.Add(fullPath);
                    }
                }
            }

            return projectPaths;
        }

        private async Task<ProjectInfo> CreateBasicProjectInfo(string projectPath)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var projectId = ProjectId.CreateNewId();

            // Parse target framework from .csproj
            var targetFramework = GetTargetFramework(projectPath);

            return ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                projectName,
                projectName,
                LanguageNames.CSharp,
                filePath: projectPath,
                compilationOptions: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    allowUnsafe: true,
                    platform: Platform.AnyCpu),
                parseOptions: new CSharpParseOptions(
                    LanguageVersion.Latest,
                    preprocessorSymbols: GetPreprocessorSymbols(projectPath)));
        }


        private void populateProjectParallel(Solution solution, object solutionLock, string projectPath, ConcurrentDictionary<string, (ProjectId, ProjectInfo)> projectRegistry, int maxParallelism)
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (!projectRegistry.TryGetValue(projectPath, out (ProjectId, ProjectInfo) projectID))
            {
                UnityEngine.Debug.LogError($"Failed to load project {projectPath}, project info could not be retrieved");
            }

            var sourceFiles = GetSourceFiles(projectPath);
            UnityEngine.Debug.Log($"[populateProjectParallel @ {projectID.Item2.Name}] found {sourceFiles.Count} sourcefiles after {stopwatch.ElapsedMilliseconds} ms");

            ConcurrentBag<DocumentInfo> allLoadedSourceFiles = new ConcurrentBag<DocumentInfo>();
            Parallel.ForEach(sourceFiles, new ParallelOptions { MaxDegreeOfParallelism = maxParallelism }, sourceFile =>
            {
                if (!File.Exists(sourceFile))
                {
                    return;
                }

                var documentId = DocumentId.CreateNewId(projectID.Item1);
                var sourceText = SourceText.From(File.ReadAllText(sourceFile));

                var documentInfo = DocumentInfo.Create(
                    documentId,
                    Path.GetFileName(sourceFile),
                    filePath: sourceFile,
                    loader: TextLoader.From(TextAndVersion.Create(sourceText, VersionStamp.Create())));

                allLoadedSourceFiles.Add(documentInfo);
            });

            UnityEngine.Debug.Log($"[populateProjectParallel @ {projectID.Item2.Name}] preloaded {sourceFiles.Count} sourcefiles after {stopwatch.ElapsedMilliseconds} ms");

            ImmutableArray<DocumentInfo> allDocs = allLoadedSourceFiles.ToArray().ToImmutableArray();
            lock (solutionLock)
            {
                solution.AddDocuments(allDocs);
            }

            UnityEngine.Debug.Log($"[populateProjectParallel @ {projectID.Item2.Name}] loaded {sourceFiles.Count} sourcefiles into solution after {stopwatch.ElapsedMilliseconds} ms");
        }





        //private async Task<Solution> PopulateProject(Solution solution, string projectPath)
        //{
        //    var projectId = _projectIdMap[projectPath];
        //    var project = solution.GetProject(projectId);

        //    // Add source files
        //    var sourceFiles = GetSourceFiles(projectPath);
        //    foreach (var sourceFile in sourceFiles)
        //    {
        //        if (File.Exists(sourceFile))
        //        {
        //            var documentId = DocumentId.CreateNewId(projectId);
        //            var sourceText = SourceText.From(await File.ReadAllTextAsync(sourceFile));

        //            var documentInfo = DocumentInfo.Create(
        //                documentId,
        //                Path.GetFileName(sourceFile),
        //                filePath: sourceFile,
        //                loader: TextLoader.From(TextAndVersion.Create(sourceText, VersionStamp.Create())));

        //            solution = solution.AddDocument(documentInfo);
        //        }
        //    }

        //    // Add assembly references
        //    var assemblyReferences = GetAssemblyReferences(projectPath);
        //    foreach (var assemblyRef in assemblyReferences)
        //    {
        //        if (File.Exists(assemblyRef))
        //        {
        //            var metadataRef = MetadataReference.CreateFromFile(assemblyRef);
        //            solution = solution.AddMetadataReference(projectId, metadataRef);
        //        }
        //    }

        //    // Add project references
        //    var projectReferences = GetProjectReferences(projectPath);
        //    foreach (var projectRef in projectReferences)
        //    {
        //        if (_projectIdMap.TryGetValue(projectRef, out var referencedProjectId))
        //        {
        //            var projectReference = new ProjectReference(referencedProjectId);
        //            solution = solution.AddProjectReference(projectId, projectReference);
        //        }
        //    }

        //    return solution;
        //}

        private List<string> GetSourceFiles(string projectPath)
        {
            var sourceFiles = new List<string>();
            var projectDir = Path.GetDirectoryName(projectPath);

            try
            {
                var doc = XDocument.Load(projectPath);

                // Get explicitly included files
                var compileElements = doc.Descendants("Compile")
                    .Concat(doc.Descendants("None").Where(e =>
                        e.Attribute("Include")?.Value.EndsWith(".cs") == true));

                foreach (var element in compileElements)
                {
                    var include = element.Attribute("Include")?.Value;
                    if (!string.IsNullOrEmpty(include) && include.EndsWith(".cs"))
                    {
                        var fullPath = Path.GetFullPath(Path.Combine(projectDir, include));
                        sourceFiles.Add(fullPath);
                    }
                }

                // If no explicit Compile elements, use SDK-style implicit includes
                if (!sourceFiles.Any())
                {
                    var allCsFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                        .Where(f => !f.Contains("bin") && !f.Contains("obj"))
                        .ToList();

                    sourceFiles.AddRange(allCsFiles);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to parse source files from {projectPath}: {ex.Message}");

                // Fallback: scan directory for .cs files
                var projectDir2 = Path.GetDirectoryName(projectPath);
                var fallbackFiles = Directory.GetFiles(projectDir2, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("bin") && !f.Contains("obj"))
                    .ToList();

                sourceFiles.AddRange(fallbackFiles);
            }

            return sourceFiles;
        }

        private List<string> GetAssemblyReferences(string projectPath)
        {
            var references = new List<string>();
            var projectDir = Path.GetDirectoryName(projectPath);

            try
            {
                var doc = XDocument.Load(projectPath);

                // Get Reference elements (old-style)
                var referenceElements = doc.Descendants("Reference");
                foreach (var element in referenceElements)
                {
                    var include = element.Attribute("Include")?.Value;
                    var hintPath = element.Element("HintPath")?.Value;

                    if (!string.IsNullOrEmpty(hintPath))
                    {
                        var fullPath = Path.GetFullPath(Path.Combine(projectDir, hintPath));
                        if (File.Exists(fullPath))
                        {
                            references.Add(fullPath);
                        }
                    }
                    else if (!string.IsNullOrEmpty(include))
                    {
                        // Try to resolve from GAC or known locations
                        var resolved = ResolveAssemblyReference(include);
                        if (!string.IsNullOrEmpty(resolved))
                        {
                            references.Add(resolved);
                        }
                    }
                }

                // Add common Unity/Framework references
                AddCommonReferences(references);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to parse assembly references from {projectPath}: {ex.Message}");
                AddCommonReferences(references);
            }

            return references.Where(File.Exists).ToList();
        }

        private void AddCommonReferences(List<string> references)
        {
            // Add .NET Standard/Core references
            var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
            var commonRefs = new[]
            {
            Path.Combine(runtimeDir, "mscorlib.dll"),
            Path.Combine(runtimeDir, "System.dll"),
            Path.Combine(runtimeDir, "System.Core.dll"),
            Path.Combine(runtimeDir, "System.Runtime.dll"),
            Path.Combine(runtimeDir, "netstandard.dll")
        };

            references.AddRange(commonRefs.Where(File.Exists));

            // Add Unity references if available
#if UNITY_EDITOR
            var unityEngine = typeof(UnityEngine.Object).Assembly.Location;
            var unityEditor = typeof(UnityEditor.Editor).Assembly.Location;

            if (File.Exists(unityEngine)) references.Add(unityEngine);
            if (File.Exists(unityEditor)) references.Add(unityEditor);
#endif
        }

        private List<string> GetProjectReferences(string projectPath)
        {
            var projectReferences = new List<string>();
            var projectDir = Path.GetDirectoryName(projectPath);

            try
            {
                var doc = XDocument.Load(projectPath);

                var projectRefElements = doc.Descendants("ProjectReference");
                foreach (var element in projectRefElements)
                {
                    var include = element.Attribute("Include")?.Value;
                    if (!string.IsNullOrEmpty(include))
                    {
                        var fullPath = Path.GetFullPath(Path.Combine(projectDir, include));
                        if (File.Exists(fullPath))
                        {
                            projectReferences.Add(fullPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to parse project references from {projectPath}: {ex.Message}");
            }

            return projectReferences;
        }

        private string GetTargetFramework(string projectPath)
        {
            try
            {
                var doc = XDocument.Load(projectPath);
                var targetFramework = doc.Descendants("TargetFramework").FirstOrDefault()?.Value
                    ?? doc.Descendants("TargetFrameworkVersion").FirstOrDefault()?.Value
                    ?? "netstandard2.1";

                return targetFramework;
            }
            catch
            {
                return "netstandard2.1"; // Unity default
            }
        }

        private IEnumerable<string> GetPreprocessorSymbols(string projectPath)
        {
            try
            {
                var doc = XDocument.Load(projectPath);
                var defineConstants = doc.Descendants("DefineConstants").FirstOrDefault()?.Value ?? "";

                var symbols = defineConstants.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                // Add Unity-specific symbols
                symbols.AddRange(new[] { "UNITY_EDITOR", "UNITY_2023_3_OR_NEWER" });

                return symbols;
            }
            catch
            {
                return new[] { "UNITY_EDITOR", "UNITY_2023_3_OR_NEWER" };
            }
        }

        private string ResolveAssemblyReference(string assemblyName)
        {
            // Simple resolution - in a real implementation, you'd use MSBuild's resolution logic
            var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
            var simpleName = assemblyName.Split(',')[0].Trim();

            var candidates = new[]
            {
            Path.Combine(runtimeDir, $"{simpleName}.dll"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Unity", "Hub", "Editor", "*", "Editor", "Data", "Managed", $"{simpleName}.dll")
        };

            return candidates.FirstOrDefault(File.Exists);
        }
    }







    public static class ManualSolutionLoader
    {

        public static async Task<Solution> LoadSolutionManuallyAsync(string solutionPath)
        {
            var workspace = new AdhocWorkspace();

            // Parse .sln file manually
            var solutionContent = await File.ReadAllTextAsync(solutionPath);
            var projectPaths = ExtractProjectPaths(solutionContent, solutionPath);

            // Create solution
            var solutionInfo = SolutionInfo.Create(
                SolutionId.CreateNewId(),
                VersionStamp.Create(),
                solutionPath);

            var solution = workspace.AddSolution(solutionInfo);

            // Add each project manually
            foreach (var projectPath in projectPaths)
            {
                solution = await AddProjectToSolution(solution, projectPath);
            }

            return solution;
        }

        private static List<string> ExtractProjectPaths(string solutionContent, string solutionPath)
        {
            var projectPaths = new List<string>();
            var solutionDir = Path.GetDirectoryName(solutionPath);

            // Regex to match project lines in .sln file
            var projectRegex = new Regex(@"Project\(""\{[^}]+\}""\)\s*=\s*""[^""]+"",\s*""([^""]+)"",");

            foreach (Match match in projectRegex.Matches(solutionContent))
            {
                var relativePath = match.Groups[1].Value;
                if (relativePath.EndsWith(".csproj"))
                {
                    var fullPath = Path.Combine(solutionDir, relativePath);
                    if (File.Exists(fullPath))
                    {
                        projectPaths.Add(fullPath);
                    }
                }
            }

            return projectPaths;
        }

        private static async Task<Solution> AddProjectToSolution(Solution solution, string projectPath)
        {
            try
            {
                // Parse .csproj file and extract information
                var projectInfo = await CreateProjectInfo(projectPath);
                return solution.AddProject(projectInfo);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to load project {projectPath}: {ex.Message}");
                return solution;
            }
        }

        private static async Task<ProjectInfo> CreateProjectInfo(string projectPath)
        {
            // This is a simplified version - you'd need to parse the .csproj XML
            // to get references, source files, etc.
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var projectId = ProjectId.CreateNewId();

            return await Task.FromResult(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                projectName,
                projectName,
                LanguageNames.CSharp,
                filePath: projectPath));
        }
    }
}













//using Microsoft.CodeAnalysis;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Threading.Tasks;
//namespace UMCP.Editor
//{
//    public class UnitySolutionService
//    {
//        private MSBuildWorkspace _workspace;
//        private Solution _solution;

//        public async Task<Solution> GetSolutionAsync()
//        {
//            if (_solution != null)
//                return _solution;

//            await LoadSolutionAsync();
//            return _solution;
//        }

//        private async Task LoadSolutionAsync()
//        {
//            try
//            {
//                // Dispose previous workspace if exists
//                _workspace?.Dispose();

//                // Create new workspace with custom properties
//                var properties = new Dictionary<string, string>
//                {
//                    // Unity-specific MSBuild properties
//                    ["UnityProjectGenerator"] = "true",
//                    ["TargetFramework"] = "netstandard2.1" // Unity's target framework
//                };

//                _workspace = MSBuildWorkspace.Create(properties);

//                // Handle workspace failures
//                _workspace.WorkspaceFailed += OnWorkspaceFailed;

//                // Get Unity solution path
//                var solutionPath = GetUnitySolutionPath();

//                // Load solution
//                _solution = await _workspace.OpenSolutionAsync(solutionPath);

//                Debug.Log($"Successfully loaded solution with {_solution.ProjectIds.Count} projects");
//            }
//            catch (Exception ex)
//            {
//                Debug.LogError($"Failed to load Unity solution: {ex}");
//                throw;
//            }
//        }

//        private string GetUnitySolutionPath()
//        {
//            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
//            var projectName = Path.GetFileName(projectRoot);
//            var expectedSolutionPath = Path.Combine(projectRoot, $"{projectName}.sln");

//            if (File.Exists(expectedSolutionPath))
//            {
//                return expectedSolutionPath;
//            }

//            // Fallback: find any .sln file
//            var solutionFiles = Directory.GetFiles(projectRoot, "*.sln");
//            if (solutionFiles.Length > 0)
//            {
//                return solutionFiles[0];
//            }

//            throw new FileNotFoundException($"No solution file found in {projectRoot}");
//        }

//        private void OnWorkspaceFailed(object sender, WorkspaceDiagnosticEventArgs e)
//        {
//            // Log but don't fail - Unity solutions often have minor issues
//            Debug.LogWarning($"Workspace diagnostic: {e.Diagnostic.Kind} - {e.Diagnostic.Message}");
//        }

//        public void Dispose()
//        {
//            _workspace?.Dispose();
//        }
//    }
//}