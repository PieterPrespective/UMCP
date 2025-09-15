using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UMCP.Editor.Tools.ManageScripts
{
    /// <summary>
    /// Utility class for finding symbol definitions in scripts and assemblies.
    /// </summary>
    public static class SymbolSearchUtility
    {
        /// <summary>
        /// Whether debug logging is enabled.
        /// </summary>
        private static bool DEBUG_LOGGING = false;

        /// <summary>
        /// Symbol types that can be searched for.
        /// </summary>
        public enum SymbolType
        {
            Unknown,
            Class,
            Struct,
            Enum,
            Interface,
            Method,
            Property,
            Field,
            Event,
            Delegate
        }

        /// <summary>
        /// Result of a symbol definition search.
        /// </summary>
        public class SymbolDefinitionResult
        {
            [JsonProperty("filepath")]
            /// <summary>
            /// Filepath of the script file where the symbol was found. Null if found in assembly.
            /// </summary>
            public string FilePath { get; set; }

            [JsonProperty("librarypath")]
            /// <summary>
            /// Library path if found in assembly, null if found in script file.
            /// </summary>
            public string LibraryPath { get; set; }

            [JsonProperty("libraryclass")]
            /// <summary>
            /// Class name if found in assembly, null if found in script file.
            /// </summary>
            public string LibraryClass { get; set; }

            [JsonProperty("linenumber")]
            /// <summary>
            /// Line number in the script file where the symbol was found. 0 if found in assembly.
            /// </summary>
            public int LineNumber { get; set; }

            [JsonProperty("foundsymboltype")]
            /// <summary>
            /// Symbol type that was found. Unknown if not determined.
            /// </summary>
            public SymbolType FoundSymbolType { get; set; } = SymbolType.Unknown;
        }

#region << SYMBOL IN SCRIPT SEARCH >>


        /// <summary>
        /// Async offthread search for symbol definitions in all script files using Roslyn.
        /// </summary>
        /// <param name="symbol">the symbol we're looking for</param>
        /// <param name="cancellationToken">cancellation token for the offthread</param>
        /// <returns></returns>
        public static async Task<List<SymbolDefinitionResult>> FindSymbolInScriptsAsync(string symbol, CancellationToken cancellationToken, int maxResults = -1)
        {
           List<SymbolDefinitionResult> result = await Task.Factory.StartNew<List<SymbolDefinitionResult>>(
           () => FindSymbolInScripts(symbol, cancellationToken, maxResults),
           cancellationToken,
           TaskCreationOptions.LongRunning,  // Creates a dedicated thread
           TaskScheduler.Default
           );

            return result;
        }

        /// <summary>
        /// Find all matches to a symbol in all script files using Roslyn for parsing.
        /// </summary>
        /// <param name="symbol">the symbol to look for</param>
        /// <returns></returns>
        public static List<SymbolDefinitionResult> FindSymbolInScripts(string symbol, CancellationToken cancellationToken = default, int maxResults = -1)
        {
            System.Diagnostics.Stopwatch stopwatch = null;

            if(DEBUG_LOGGING)
            {
                stopwatch = System.Diagnostics.Stopwatch.StartNew();
            }

            var scriptFiles = ScriptSearchUtility.GetAllScriptFiles();
            if(DEBUG_LOGGING)
            {
                Debug.Log($"Searching for symbol '{symbol}' in {scriptFiles.Count} script files using Roslyn, file index took: {stopwatch.ElapsedMilliseconds}...");
            }
            

            // Parse the symbol to determine what we're looking for
            var symbolParts = ParseSymbol(symbol);

            // Use a thread-safe collection for results
            ConcurrentBag<SymbolDefinitionResult> foundResults = new ConcurrentBag<SymbolDefinitionResult>();

            //Walk over all script files in parallel to speed up the search
            Parallel.ForEach(scriptFiles, (scriptFile, parContext) => {



                if(cancellationToken.IsCancellationRequested)
                {
                    parContext.Break();
                    return;
                }

                if(maxResults > 0 && foundResults.Count >= maxResults)
                {
                    parContext.Break();
                    return;
                }

                if (string.IsNullOrEmpty(scriptFile))
                {
                    return;
                }

                string fileContent = File.ReadAllText(scriptFile);
                if(string.IsNullOrEmpty(fileContent))
                {
                    return;
                }

                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(fileContent);
                if(syntaxTree == null)
                    {
                    return;
                }

                CompilationUnitSyntax root = syntaxTree.GetRoot() as CompilationUnitSyntax;
                if(root == null)
                    {
                    return;
                }

                analyzeMembers(root.Members, new memberSymbolSearchContext() { SymbolParts = symbolParts, FilePath = scriptFile }, foundResults);
            });

            return foundResults.ToList();
        }

        /// <summary>
        /// Search context for our member tree walker
        /// </summary>
        private struct memberSymbolSearchContext
        {
            /// <summary>
            /// Symbol parts we're looking for
            /// </summary>
            public SymbolParts SymbolParts;

            /// <summary>
            /// Filepath where we're looking for the symbol
            /// </summary>
            public string FilePath;
        }


        /// <summary>
        /// Returns whether the given memberID matches the found symbol based on its type and parts.
        /// </summary>
        /// <param name="memberID">the ID of the c# member</param>
        /// <param name="foundSymbol">the symboltype found</param>
        /// <param name="SymbolParts">description of the symbol we're looking for</param>
        /// <returns>whether the given symbol is a (partial) match</returns>
        private static bool isSymbolMatch(string memberID, SymbolType foundSymbol, SymbolParts SymbolParts)
        {
            if(string.IsNullOrEmpty(memberID))
            {
                return false;
            }

            switch (foundSymbol)
            { 
            case SymbolType.Struct:
            case SymbolType.Class:

                    //Match on either classname or Name if either is set
                    return (!string.IsNullOrEmpty(SymbolParts.ClassName) && (memberID == SymbolParts.ClassName) ||
                           (!string.IsNullOrEmpty(SymbolParts.Name) && (memberID == SymbolParts.Name)));

            case SymbolType.Property:
            case SymbolType.Enum:
            case SymbolType.Field:
            case SymbolType.Event:
            case SymbolType.Method:
            case SymbolType.Delegate:
                return (memberID == SymbolParts.Name);
            case SymbolType.Unknown:
                return false;
            default:
                UnityEngine.Debug.LogWarning($"encountered unexpected Symbol type {foundSymbol}");
                break;
            }
            return false;
        }

        /// <summary>
        /// Analyize the members of a syntax tree recursively to find symbol definitions.
        /// </summary>
        /// <param name="_members">members to look through</param>
        /// <param name="_searchContext">context data for the search</param>
        /// <param name="_results">reference to any found results</param>
        private static void analyzeMembers(SyntaxList<MemberDeclarationSyntax> _members, memberSymbolSearchContext _searchContext, ConcurrentBag<SymbolDefinitionResult> _results)
        {

            foreach(MemberDeclarationSyntax decl in _members)
            {
                switch(decl)
                {
                    //CASE : Namespace found
                    case NamespaceDeclarationSyntax nameSpaceDeclaration:

                        if (!string.IsNullOrEmpty(_searchContext.SymbolParts.Namespace) && !nameSpaceDeclaration.Name.ToString().Contains(_searchContext.SymbolParts.Namespace))
                        {
                            continue;
                        }

                        analyzeMembers(nameSpaceDeclaration.Members, _searchContext, _results);
                        break;
                    //CASE : Class found
                    case ClassDeclarationSyntax classDeclarationSyntax:

                        string idClass = classDeclarationSyntax.Identifier.ToString();
                        if (!isSymbolMatch(idClass, SymbolType.Class, _searchContext.SymbolParts))
                        {
                            continue;
                        }

                        if(!string.IsNullOrEmpty(_searchContext.SymbolParts.Name) && _searchContext.SymbolParts.Name == idClass)
                        {
                            _results.Add(new SymbolDefinitionResult()
                            {
                                FilePath = _searchContext.FilePath,
                                LineNumber = classDeclarationSyntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                FoundSymbolType = SymbolType.Class
                            });
                        }
                        else
                        {
                            analyzeMembers(classDeclarationSyntax.Members, _searchContext, _results);
                        }

                            
                        break;
                    //CASE : Struct found
                    case StructDeclarationSyntax structDeclarationSyntax:

                        string idStruct = structDeclarationSyntax.Identifier.ToString();
                        if (!isSymbolMatch(idStruct, SymbolType.Struct, _searchContext.SymbolParts))
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(_searchContext.SymbolParts.Name) && _searchContext.SymbolParts.Name == idStruct)
                        {
                            _results.Add(new SymbolDefinitionResult()
                            {
                                FilePath = _searchContext.FilePath,
                                LineNumber = structDeclarationSyntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                FoundSymbolType = SymbolType.Struct
                            });
                        }

                        analyzeMembers(structDeclarationSyntax.Members, _searchContext, _results);
                        break;

                    //CASE : Enum found
                    case EnumDeclarationSyntax enumDeclarationSyntax:

                        if (!isSymbolMatch(enumDeclarationSyntax.Identifier.ToString(), SymbolType.Enum, _searchContext.SymbolParts))
                        {
                            continue;
                        }

                        _results.Add(new SymbolDefinitionResult()
                        {
                            FilePath = _searchContext.FilePath,
                            LineNumber = enumDeclarationSyntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            FoundSymbolType = SymbolType.Enum
                        });

                        break;

                    //CASE : Method found
                    case MethodDeclarationSyntax methodDeclarationSyntax:

                        if (!isSymbolMatch(methodDeclarationSyntax.Identifier.ToString(), SymbolType.Method, _searchContext.SymbolParts))
                        {
                            continue;
                        }

                        _results.Add(new SymbolDefinitionResult()
                        {
                            FilePath = _searchContext.FilePath,
                            LineNumber = methodDeclarationSyntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            FoundSymbolType = SymbolType.Method
                        });

                        break;

                    //CASE : Property found
                    case PropertyDeclarationSyntax propertyDeclarationSyntax:

                        if (!isSymbolMatch(propertyDeclarationSyntax.Identifier.ToString(), SymbolType.Property, _searchContext.SymbolParts))
                        {
                            continue;
                        }

                        _results.Add(new SymbolDefinitionResult()
                        {
                            FilePath = _searchContext.FilePath,
                            LineNumber = propertyDeclarationSyntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            FoundSymbolType = SymbolType.Property
                        });
                        break;

                    //CASE : Field found
                    case FieldDeclarationSyntax fieldDeclarationSyntax:
                            foreach (var variable in fieldDeclarationSyntax.Declaration.Variables)
                            {
                                if (!isSymbolMatch(variable.Identifier.ToString(), SymbolType.Field, _searchContext.SymbolParts))
                                {
                                    continue;
                                }

                                _results.Add(new SymbolDefinitionResult()
                                {
                                    FilePath = _searchContext.FilePath,
                                    LineNumber = variable.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                    FoundSymbolType = SymbolType.Field
                                });
                            }
                        break;

                    //CASE : Event found
                    case EventDeclarationSyntax eventDeclarationSyntax:

                            if (!isSymbolMatch(eventDeclarationSyntax.Identifier.ToString(), SymbolType.Event, _searchContext.SymbolParts))
                                {
                                continue;
                                }


                            _results.Add(new SymbolDefinitionResult()
                            {
                                FilePath = _searchContext.FilePath,
                                LineNumber = eventDeclarationSyntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                FoundSymbolType = SymbolType.Event
                            });
                        break;

                    //CASE : EventField found
                    case EventFieldDeclarationSyntax eventFieldDeclarationSyntax:
                            foreach (var variable in eventFieldDeclarationSyntax.Declaration.Variables)
                            {
                                if (!isSymbolMatch(variable.Identifier.ToString(), SymbolType.Event, _searchContext.SymbolParts))
                                {
                                continue;
                                }


                                _results.Add(new SymbolDefinitionResult()
                                {
                                    FilePath = _searchContext.FilePath,
                                    LineNumber = variable.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                    FoundSymbolType = SymbolType.Event
                                });
                        }
                        break;
                    //CASE : Delegate found
                    case DelegateDeclarationSyntax delegateDeclarationSyntax:
                            
                            if (!isSymbolMatch(delegateDeclarationSyntax.Identifier.ToString(), SymbolType.Delegate, _searchContext.SymbolParts))
                                {
                                continue;
                                }

                            _results.Add(new SymbolDefinitionResult()
                            {
                                FilePath = _searchContext.FilePath,
                                LineNumber = delegateDeclarationSyntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                FoundSymbolType = SymbolType.Delegate
                            });
                    break;
                    case IncompleteMemberSyntax incompleteMemberSyntax:
                        // Not implemented
                    break;
                    case GlobalStatementSyntax globalStatementSyntax:
                    // Not implemented
                    break;
                    case BaseTypeDeclarationSyntax baseTypeDeclarationSyntax:
                        // Not implemented
                        //analyzeMembers(baseTypeDeclarationSyntax., _analysisContext, ref _results);
                        break;
                }
            }
        }

        #endregion
        #region << SYMBOL IN ASSEMBLY SEARCH >>

        ///// <summary>
        ///// Searches for symbol definition in loaded assemblies using project file references.
        ///// </summary>
        //private static SymbolDefinitionResult FindInAssemblies(string symbol)
        //{
        //    try
        //    {
        //        // First try the enhanced project-based assembly discovery
        //        var projectBasedResult = FindInProjectReferencedAssemblies(symbol);
        //        if (projectBasedResult != null)
        //        {
        //            return projectBasedResult;
        //        }

        //        // Fallback to existing methods
        //        return FindInLoadedAssemblies(symbol);
        //    }
        //    catch (Exception e)
        //    {
        //        Debug.LogError($"[SymbolSearchUtility] Error searching in assemblies: {e.Message}");
        //    }

        //    return null;
        //}

        public static async Task<List<SymbolDefinitionResult>> FindSymbolDefinitionAsync(string symbol, CancellationToken cancellationToken, int maxResults = -1)
        {
            List<SymbolDefinitionResult> result = await Task.Factory.StartNew<List<SymbolDefinitionResult>>(
            () => FindSymbolInAssembliesAndFiles(symbol, cancellationToken, maxResults),
            cancellationToken,
            TaskCreationOptions.LongRunning,  // Creates a dedicated thread
            TaskScheduler.Default
            );

            return result;
        }




        /// <summary>
        /// Searches for symbol definition in project-referenced assemblies using .csproj files.
        /// </summary>
        private static List<SymbolDefinitionResult> FindSymbolInAssembliesAndFiles(string symbol, CancellationToken cancellationToken = default, int maxResults = -1)
        {
            List<SymbolDefinitionResult> results = new List<SymbolDefinitionResult>();
            try
            {
                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

                //1) Get the project root directory
                string projectPath = Directory.GetParent(Application.dataPath).FullName;

                //2) Look for .csproj files that likely contain Unity references
                var projectFiles = Directory.GetFiles(projectPath, "*.csproj", SearchOption.TopDirectoryOnly)
                    .ToList();

                //3) Get all Assemblies used in this project - including those not actually within the project directory (e.g. Unity core assemblies)
                ConcurrentDictionary<string, string> allAssemblied = new ConcurrentDictionary<string, string>();
                Parallel.ForEach(projectFiles, (f) =>
                {
                    AppendAssembliesFromProjectFile(allAssemblied, f);
                });

                //4) Seperate out the external assemblies since we'll find the symbols in project scripts directly
                var externalAssemblies = allAssemblied
                    .Where(kvp =>
                    {
                        string normalizedPath = kvp.Key.Replace('\\', '/');
                        bool isInScriptAssemblies = normalizedPath.Contains("Library/ScriptAssemblies");
                        return !isInScriptAssemblies;
                    })
                    .ToList();

                //5) Look for the symbol in scripts
                results.AddRange(FindSymbolInScripts(symbol, cancellationToken) ?? new List<SymbolDefinitionResult>());

                if (maxResults > 0 && results.Count >= maxResults)
                {
                    return results.Take(maxResults).ToList();
                }

                //6) Look for the symbol in external assemblies
                ConcurrentBag<SymbolDefinitionResult> foundFiles = new ConcurrentBag<SymbolDefinitionResult>();
                SymbolParts symbolParts = ParseSymbol(symbol);

                Debug.Log($"[SymbolSearchUtility] Project-based assembly discovery found {externalAssemblies.Count} external assemblies in {stopwatch.ElapsedMilliseconds}ms, searching for symbol '{symbol}' ({symbolParts})...");


                Parallel.ForEach(externalAssemblies, (f, state, idx) =>
                {
                    if(cancellationToken.IsCancellationRequested)
                    {
                        state.Break();
                    }

                    if (maxResults > 0 && (foundFiles.Count + results.Count) >= maxResults)
                    {
                        state.Break();
                        return;
                    }

                    try
                    {
                        var assembly = System.Reflection.Assembly.LoadFrom(f.Key);
                        SearchSymbolInAssembly(assembly, symbolParts, foundFiles);
                    }
                    catch (Exception e)
                    {
                        if (DEBUG_LOGGING)
                        {
                            Debug.Log($"[SymbolSearchUtility] Could not load external assembly {f.Key}: {e.Message}");
                        }
                    }
                });

                results.AddRange(foundFiles.ToList() ?? new List<SymbolDefinitionResult>());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SymbolSearchUtility] Error in project-based assembly discovery: {e.Message}");
            }
            return results;
        }

        /// <summary>
        /// Try and append any assemblies found in a specific unity project file
        /// </summary>
        /// <param name="assemblyPaths"></param>
        /// <param name="projectFilePath"></param>
        private static void AppendAssembliesFromProjectFile(ConcurrentDictionary<string, string> assemblyPaths, string projectFilePath)
        {
            try
            {
                string projectContent = File.ReadAllText(projectFilePath);

                // Parse assembly references using regex (simpler than full XML parsing)
                var referencePattern = new Regex(@"<Reference Include=""([^""]+)"">.*?<HintPath>([^<]+)</HintPath>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);

                var matches = referencePattern.Matches(projectContent);

                foreach (Match match in matches)
                {
                    assemblyPaths.TryAdd(match.Groups[2].Value, match.Groups[1].Value);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SymbolSearchUtility] Error parsing project file {projectFilePath}: {e.Message}");
            }
        }


        /// <summary>
        /// Searches for a symbol in an assembly using reflection.
        /// </summary>
        private static void SearchSymbolInAssembly(System.Reflection.Assembly assembly, SymbolParts symbolParts, ConcurrentBag<SymbolDefinitionResult> _results)
        {
            try
            {
                var types = assembly.GetTypes();
                //var symbolParts = ParseSymbol(symbol);

                foreach (var type in types)
                {
                    // Check if type matches
                    if (IsTypeMatch(type, symbolParts))
                    {
                        _results.Add(new SymbolDefinitionResult
                        {
                            LibraryPath = GetRelativeAssemblyPath(assembly.Location),
                            LibraryClass = type.FullName,
                            LineNumber = 0, // Not applicable for compiled assemblies
                            FoundSymbolType = (type.IsValueType) ? SymbolType.Struct : SymbolType.Class
                        });
                    }

                    // Check members of the type
                    else if (HasMemberMatch(type, symbolParts, out SymbolType foundType))
                    {
                        _results.Add(new SymbolDefinitionResult
                        {
                            LibraryPath = GetRelativeAssemblyPath(assembly.Location),
                            LibraryClass = type.FullName,
                            LineNumber = 0,
                            FoundSymbolType = foundType
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SymbolSearchUtility] Error searching assembly {assembly.FullName}: {e.Message}");
            }
        }

        /// <summary>
        /// Checks if a type matches the symbol parts.
        /// </summary>
        private static bool IsTypeMatch(Type type, SymbolParts symbolParts)
        {
            if (type.Name == symbolParts.Name)
            {
                if (!string.IsNullOrEmpty(symbolParts.Namespace))
                {
                    return type.Namespace.Contains(symbolParts.Namespace);
                }
                else if(!string.IsNullOrEmpty(symbolParts.ClassName))
                {
                    return type.Namespace.Contains(symbolParts.ClassName);
                }
                 return true;
            }

            //if (!string.IsNullOrEmpty(symbolParts.ClassName) && type.Name == symbolParts.ClassName)
            //{
            //    if (!string.IsNullOrEmpty(symbolParts.Namespace))
            //    {
            //        return type.Namespace.Contains(symbolParts.Namespace);
            //    }
            //    return true;
            //}

            return false;
        }

        /// <summary>
        /// Checks if a type has a member matching the symbol parts.
        /// </summary>
        private static bool HasMemberMatch(Type type, SymbolParts symbolParts, out SymbolType symbolMatch)
        {
            symbolMatch = SymbolType.Unknown;
            // If we have a class name specified and it doesn't match, skip
            if (!string.IsNullOrEmpty(symbolParts.ClassName) && type.Name != symbolParts.ClassName)
            {
            return false;
            }

            //If we have a namespace specified and it doesn't match, skip
            if (!string.IsNullOrEmpty(symbolParts.Namespace) && !type.Namespace.Contains(symbolParts.Namespace))
            {
            return false;
            }


            var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

            // Check methods
            var methods = type.GetMethods(bindingFlags);
            if (methods.Any(m => m.Name == symbolParts.Name))
            {
                symbolMatch = SymbolType.Method;
                return true;
            }

            // Check properties
            var properties = type.GetProperties(bindingFlags);
            if (properties.Any(p => p.Name == symbolParts.Name))
            {
                symbolMatch = SymbolType.Property;
                return true;
            }

            // Check fields
            var fields = type.GetFields(bindingFlags);
            if (fields.Any(f => f.Name == symbolParts.Name))
            {
                symbolMatch = SymbolType.Field;
                return true;
            }

            // Check events
            var events = type.GetEvents(bindingFlags);
            if (events.Any(e => e.Name == symbolParts.Name))
            {
                symbolMatch = SymbolType.Event;
                return true;
            }

            // Check nested types
            var nestedTypes = type.GetNestedTypes(bindingFlags);
            if (nestedTypes.Any(t => t.Name == symbolParts.Name))
            {
                symbolMatch = SymbolType.Class;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a relative path for an assembly location.
        /// </summary>
        private static string GetRelativeAssemblyPath(string absolutePath)
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            if (absolutePath.StartsWith(projectPath))
            {
                return absolutePath.Substring(projectPath.Length + 1).Replace('\\', '/');
            }
            return absolutePath.Replace('\\', '/');
        }



        #endregion


        /// <summary>
        /// Parses a symbol string to extract namespace, class, and member information.
        /// </summary>
        private static SymbolParts ParseSymbol(string symbol)
        {
            var parts = new SymbolParts();
            
            // Handle fully qualified names (e.g., Namespace.Class.Member)
            var lastDotIndex = symbol.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                parts.Namespace = symbol.Substring(0, lastDotIndex);
                parts.Name = symbol.Substring(lastDotIndex + 1);
                
                // Check if namespace contains class name
                var namespaceParts = parts.Namespace.Split('.');
                if (namespaceParts.Length > 0)
                {
                    // Assume last part might be class name if it starts with uppercase
                    var lastPart = namespaceParts[namespaceParts.Length - 1];
                    if (lastPart.Length > 0 && char.IsUpper(lastPart[0]))
                    {
                        parts.ClassName = lastPart;
                        if (namespaceParts.Length > 1)
                        {
                            parts.Namespace = string.Join(".", namespaceParts.Take(namespaceParts.Length - 1));
                        }
                        else
                        {
                            parts.Namespace = null;
                        }
                    }
                }
            }
            else
            {
                parts.Name = symbol;
            }
            
            return parts;
        }


        /// <summary>
        /// Helper class to store parsed symbol parts.
        /// </summary>
        private class SymbolParts
        {
            public string Namespace { get; set; }
            public string ClassName { get; set; }
            public string Name { get; set; }

            public override string ToString()
            {
                return $"Namespace: {Namespace}, ClassName: {ClassName}, Name: {Name}";
            }
        }
    }
}