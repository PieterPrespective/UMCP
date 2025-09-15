using Newtonsoft.Json.Linq;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UMCP.Editor.Helpers;
using UMCP.Editor.Settings;
using UnityEditor;
using UnityEngine;
using static UMCP.Editor.Tools.ManageScripts.SymbolSearchUtility;

namespace UMCP.Editor.Tools.ManageScripts
{
    /// <summary>
    /// Handles script analysis and manipulation operations including searching, finding symbol definitions, and decompiling.
    /// </summary>
    public static class ManageScripts
    {

        //public static async Task<object> HandleCommand(JObject @params)
        //{
        //    Debug.Log($"[ManageScripts] HandleCommand called with params: {@params.ToString()}");

        //    object result = await Task.Factory.StartNew<object>(async
        //    () => await HandleCommandOffthread(@params),
        //    new CancellationTokenSource().Token,
        //    TaskCreationOptions.LongRunning,  // Creates a dedicated thread
        //    TaskScheduler.Default
        //    );

        //    Debug.Log($"[ManageScripts] HandleCommand completed with result: {result.ToString()}");

        //    return result;
        //}


        /// <summary>
        /// Main handler for ManageScripts commands following the HandleCommand pattern.
        /// </summary>
        /// <param name="params">JObject containing action and parameters for the operation</param>
        /// <returns>Response object with operation result</returns>
        public static async Task<object> HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();
            if (string.IsNullOrEmpty(action))
            {
                return Response.Error("Action parameter is required.");
            }

            try
            {
                switch (action)
                {
                    //case "findinscriptfiles":
                    //case "find_in_script_files":
                    //    return FindInScriptFiles(@params);
                    
                    case "findsymboldefinition":
                    case "find_symbol_definition":
                        return await FindSymbolDefinition(@params);
                    
                    case "decompileclass":
                    case "decompile_class":
                        return DecompileClass(@params);
                    
                    default:
                        return Response.Error($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageScripts] Action '{action}' failed: {e}");
                return Response.Error($"Internal error processing action '{action}': {e.Message}");
            }
        }

        /// <summary>
        /// Searches through loaded assemblies and script files for text patterns.
        /// </summary>
        private static object FindInScriptFiles(JObject @params)
        {
            string searchFilter = @params["SearchFilter"]?.ToString();
            if (string.IsNullOrEmpty(searchFilter))
            {
                return Response.Error("SearchFilter parameter is required.");
            }

            bool matchCase = @params["MatchCase"]?.ToObject<bool>() ?? false;
            bool matchWholeWord = @params["MatchWholeWord"]?.ToObject<bool>() ?? false;
            bool useRegularExpression = @params["UseRegularExpression"]?.ToObject<bool>() ?? false;

            try
            {
                var results = ScriptSearchUtility.SearchInScriptFiles(
                    searchFilter, 
                    matchCase, 
                    matchWholeWord, 
                    useRegularExpression
                );

                var formattedResults = results.Select(r => new
                {
                    FilePath = r.FilePath,
                    LineNo = r.LineNumber,
                    LineContent = r.LineContent
                }).ToList();

                return Response.Success(
                    $"Found {formattedResults.Count} matches for '{searchFilter}'.", 
                    formattedResults
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Error searching script files: {e.Message}");
            }
        }

        /// <summary>
        /// Finds the definition of a given script symbol.
        /// </summary>
        private static async Task<object> FindSymbolDefinition(JObject @params)
        {
            string symbol = @params["Symbol"]?.ToString();
            int noOfResults = @params["NoOfResults"]?.ToObject<int>() ?? 1;
            if (string.IsNullOrEmpty(symbol))
            {
                return Response.Error("Symbol parameter is required.");
            }

            try
            {
                // Generate unique GUID for this search operation
                string guid = Guid.NewGuid().ToString();
                
                // Start the async search operation
                FindSymbolAsync(guid, symbol, noOfResults);
                
                // Immediately return success with status="running" and GUID
                return await Task.FromResult(new
                {
                    success = true,
                    message = $"Symbol definition search started for '{symbol}'.",
                    status = "running",
                    data = new JObject
                    {
                        ["guid"] = guid,
                        ["message"] = $"Searching for symbol definition '{symbol}'. Results will appear in Unity console.",
                        ["status"] = "running"
                    }
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Error finding symbol definition: {e.Message}");
            }
        }

        private static async void FindSymbolAsync(string guid, string symbol, int noOfResults)
        {
            try
            {
                // Mark the start of the search
                Debug.Log($"[FindSymbolDefinition] Starting search for symbol '{symbol}' with GUID: {guid}");
                
                List<SymbolDefinitionResult> results = await SymbolSearchUtility.FindSymbolDefinitionAsync(symbol, new CancellationTokenSource().Token, noOfResults);
                
                // Output results in a parseable format for the server
                Debug.Log($"[FindSymbolDefinition] Search completed for GUID: {guid}");
                
                // Create JSON output for results
                var resultArray = new JArray();
                foreach (var r in results)
                {
                    resultArray.Add(new JObject
                    {
                        ["LibraryPath"] = r.LibraryPath ?? "",
                        ["LibraryClass"] = r.LibraryClass ?? "",
                        ["FilePath"] = r.FilePath ?? "",
                        ["LineNumber"] = r.LineNumber,
                        ["FoundSymbolType"] = r.FoundSymbolType.ToString()
                    });
                }
                
                // Output the completion message with results
                var completionData = new JObject
                {
                    ["guid"] = guid,
                    ["status"] = "completed",
                    ["resultCount"] = results.Count,
                    ["results"] = resultArray
                };

                string completionMessage = completionData.ToString(Newtonsoft.Json.Formatting.None);
                completionMessage.Replace('\r', ' ').Replace('\n', ' ');


                Debug.Log($"[FindSymbolDefinition-{guid}] COMPLETION_DATA: |$$^$$|\"{completionData.ToString(Newtonsoft.Json.Formatting.None).Replace('\r', ' ').Replace('\n', ' ')}\"|$$^$$|");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FindSymbolDefinition-{guid}] ERROR: {e.Message}");
                
                var errorData = new JObject
                {
                    ["guid"] = guid,
                    ["status"] = "error",
                    ["error"] = e.Message
                };
                
                Debug.Log($"[FindSymbolDefinition-{guid}] COMPLETION_DATA: {errorData.ToString(Newtonsoft.Json.Formatting.None)}");
            }
        }




        /// <summary>
        /// Decompiles a class from a Dynamic Link Library.
        /// </summary>
        private static object DecompileClass(JObject @params)
        {
            string libraryPath = @params["LibraryPath"]?.ToString();
            string libraryClass = @params["LibraryClass"]?.ToString();
            string outputFileName = @params["OutputFileName"]?.ToString();
            bool overrideExisting = @params["OverrideExistingFile"]?.Value<bool>() ?? false;

            // If no output filename is provided, generate one based on class name and settings
            if (string.IsNullOrEmpty(outputFileName))
            {
                // Get output folder from settings
                string outputFolder = UMCPSettings.Instance.DecompiledScriptsOutputFolder;
                if (string.IsNullOrEmpty(outputFolder))
                {
                    outputFolder = "UMCP/DecompiledScripts";
                }
                
                // Ensure output folder exists
                string fullOutputPath = Path.Combine(Application.dataPath, outputFolder);
                if (!Directory.Exists(fullOutputPath))
                {
                    Directory.CreateDirectory(fullOutputPath);
                }
                
                // Generate filename from class name
                string fileName = libraryClass?.Replace('.', '_') ?? "DecompiledClass";
                if (!fileName.EndsWith(".cs"))
                {
                    fileName += ".cs";
                }
                
                outputFileName = Path.Combine(fullOutputPath, fileName);
            }
            else
            {
                // Make sure the provided path is absolute
                outputFileName = Path.GetFullPath(outputFileName);
            }

            Debug.Log("[UMCP.DecompileClass] Attempting to decompile class '" + libraryClass + "' from library '" + libraryPath + "' to file: " + outputFileName);

            try
            {
                bool decompileResult = DecompilerUtility.DecompileClass(
                    libraryPath, 
                    libraryClass, 
                    outputFileName,
                    out string resultMessage,
                    overrideExisting);

                if(!decompileResult)
                {
                    return Response.Error(resultMessage);
                }

                var result = new
                {
                    FilePath = outputFileName
                };

                return Response.Success(
                    resultMessage, 
                    result
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Error decompiling class: {e.Message}");
            }
        }
    }
}