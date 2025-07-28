using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;
using UMCPServer.Services;

namespace UMCPServer.Tools;

/// <summary>
/// MCP tool for running tests via Unity Test Runner
/// </summary>
[McpServerToolType]
public class RunTestsTool
{
    private readonly ILogger<RunTestsTool> _logger;
    private readonly IUnityConnectionService _unityConnection;
    
    public RunTestsTool(ILogger<RunTestsTool> logger, IUnityConnectionService unityConnection)
    {
        _logger = logger;
        _unityConnection = unityConnection;
    }
    
    /// <summary>
    /// Runs specified tests in the Unity Test Runner
    /// </summary>
    [McpServerTool]
    [Description("Run the given tests in the Unity3D Testrunner. Automatically marks a new step for logging.")]
    public async Task<object> RunTests(
        [Description("The mode of the tests to run; either 'EditMode', 'PlayMode' or 'All'")]
        string TestMode = "All",
        
        [Description("If not empty, only run the tests provided (format: 'MyTestClass.NameOfMyTest'). If empty, run all tests matching testmode")]
        string[]? Filter = null,
        
        [Description("Whether to output TestResults")]
        bool OutputTestResults = true,
        
        [Description("Whether to output LogData")]
        bool OutputLogData = true,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Running Unity tests with mode: {TestMode}, filter count: {FilterCount}", 
                TestMode, Filter?.Length ?? 0);
            
            if (!_unityConnection.IsConnected && !await _unityConnection.ConnectAsync())
            {
                return new
                {
                    success = false,
                    error = "Unity Editor is not running or MCP Bridge is not available. Please ensure Unity Editor is open and the UMCP Unity3D Client is active."
                };
            }
            
            // Validate TestMode
            if (!IsValidTestMode(TestMode))
            {
                return new
                {
                    success = false,
                    error = $"Invalid TestMode: '{TestMode}'. Valid values are 'EditMode', 'PlayMode', or 'All'."
                };
            }
            
            // Build parameters for the Unity command
            var parameters = new JObject
            {
                ["TestMode"] = TestMode,
                ["OutputTestResults"] = OutputTestResults,
                ["OutputLogData"] = OutputLogData
            };
            
            if (Filter != null && Filter.Length > 0)
            {
                parameters["Filter"] = new JArray(Filter);
            }
            else
            {
                parameters["Filter"] = new JArray();
            }
            
            // Send command to Unity - this will start the tests
            var response = await _unityConnection.SendCommandAsync("run_tests", parameters, cancellationToken);
            
            if (response == null)
            {
                return new
                {
                    success = false,
                    error = "Failed to get response from Unity"
                };
            }
            
            // Unity can return the response directly or wrapped in a result object
            // Check if this is a direct response from Unity (has success/error at root)
            var directSuccess = response.Value<bool?>("success");
            var directError = response.Value<string>("error");
            
            // If we have a direct error response
            if (directSuccess == false && !string.IsNullOrEmpty(directError))
            {
                return new
                {
                    success = false,
                    error = directError
                };
            }

            // If we have a direct success response with running status
            if (directSuccess == true && response.Value<string>("status") == "running")
            {
                _logger.LogInformation("Tests started running in Unity, waiting for completion...");

                // Extract the step GUID from Unity's response (it's embedded in the logs)
                // We'll need to wait and then request the step logs
                await Task.Delay(2000, cancellationToken); // Initial delay to let tests start

                // Poll for test completion with exponential backoff
                var maxAttempts = 30; // Maximum 30 attempts
                var delayMs = 1000; // Start with 1 second
                var maxDelayMs = 5000; // Max 5 seconds between attempts
                var completed = false;
                JObject completionData = null;

                for (int attempt = 0; attempt < maxAttempts && !completed; attempt++)
                {
                    // Check if tests are still running by querying the console
                    var consoleParams = new JObject
                    {
                        ["action"] = "get",
                        ["types"] = new JArray("log"),
                        ["filterText"] = "[RunTests]",
                        ["count"] = 20,
                        ["format"] = "detailed"
                    };

                    var consoleResponse = await _unityConnection.SendCommandAsync("read_console", consoleParams, cancellationToken);

                    if (consoleResponse != null && consoleResponse["success"]?.Value<bool>() == true)
                    {
                        var entries = consoleResponse["data"] as JArray;
                        if (entries != null)
                        {
                            // Look for completion message
                            foreach (var entry in entries.Reverse()) // Check newest first
                            {
                                string message;

                                // Handle both plain format (string) and detailed format (object with message property)
                                if (entry.Type == JTokenType.String)
                                {
                                    // Plain format: entry is a direct string
                                    message = entry.Value<string>() ?? "";
                                }
                                else
                                {
                                    // Detailed format: entry is an object with message property
                                    message = entry["message"]?.Value<string>() ?? "";
                                }

                                if (message.Contains("[RunTests] TEST_EXECUTION_COMPLETED"))
                                {
                                    _logger.LogInformation("Test execution completed, parsing results...");

                                    // Parse the results from the console logs
                                    var testResultFilePaths = new List<string>();
                                    var allSuccess = true;
                                    var testCount = 0;

                                    foreach (var logEntry in entries)
                                    {
                                        string logMessage;

                                        // Handle both plain format (string) and detailed format (object with message property)
                                        if (logEntry.Type == JTokenType.String)
                                        {
                                            // Plain format: entry is a direct string
                                            logMessage = logEntry.Value<string>() ?? "";
                                        }
                                        else
                                        {
                                            // Detailed format: entry is an object with message property
                                            logMessage = logEntry["message"]?.Value<string>() ?? "";
                                        }

                                        // Parse success/failure messages to determine overall success
                                        if (logMessage.Contains("[RunTests] ✗ FAIL:"))
                                        {
                                            allSuccess = false;
                                        }

                                        // Check the completion summary
                                        if (logMessage.Contains("[RunTests] Test execution completed."))
                                        {
                                            // Parse format: "[RunTests] Test execution completed. AllSuccess: True, Tests: 0"
                                            allSuccess = logMessage.Contains("AllSuccess: True");

                                            // Extract test count
                                            var testCountMatch = System.Text.RegularExpressions.Regex.Match(logMessage, @"Tests: (\d+)");
                                            if (testCountMatch.Success && int.TryParse(testCountMatch.Groups[1].Value, out int parsedTestCount))
                                            {
                                                testCount = parsedTestCount;
                                            }
                                        }
                                        
                                        // Check for test result file paths
                                        if (logMessage.Contains("[RunTests] TEST_RESULTS_FILE_PATH:"))
                                        {
                                            var pathStart = logMessage.IndexOf("TEST_RESULTS_FILE_PATH:") + "TEST_RESULTS_FILE_PATH:".Length;
                                            var path = logMessage.Substring(pathStart).Trim();
                                            if (!string.IsNullOrEmpty(path))
                                            {
                                                testResultFilePaths.Add(path);
                                            }
                                        }
                                        
                                        // Check for all test result file paths (when running All mode)
                                        if (logMessage.Contains("[RunTests] TEST_RESULTS_FILE_PATHS_ALL:"))
                                        {
                                            var pathsStart = logMessage.IndexOf("TEST_RESULTS_FILE_PATHS_ALL:") + "TEST_RESULTS_FILE_PATHS_ALL:".Length;
                                            var pathsString = logMessage.Substring(pathsStart).Trim();
                                            if (!string.IsNullOrEmpty(pathsString))
                                            {
                                                // Clear existing paths and use the combined list
                                                testResultFilePaths.Clear();
                                                testResultFilePaths.AddRange(pathsString.Split(';').Where(p => !string.IsNullOrEmpty(p)));
                                            }
                                        }
                                    }

                                    // Test execution completed successfully (tool ran without errors)
                                    // Return test result files regardless of whether individual tests passed or failed
                                    if (testResultFilePaths.Count > 0)
                                    {
                                        completionData = new JObject
                                        {
                                            ["success"] = true,
                                            ["testResultFiles"] = JArray.FromObject(testResultFilePaths),
                                            ["message"] = testResultFilePaths.Count == 1 
                                                ? $"Tests completed. Results saved to: {testResultFilePaths[0]}. Use InterpretTestResults tool to analyze the results."
                                                : $"Tests completed. Results saved to {testResultFilePaths.Count} files. Use InterpretTestResults tool to analyze the results.",
                                            ["testCount"] = testCount,
                                            ["allTestsPassed"] = allSuccess
                                        };
                                    }
                                    else
                                    {
                                        // No test result files were generated, but tool execution succeeded
                                        completionData = new JObject
                                        {
                                            ["success"] = true,
                                            ["message"] = "Test execution completed but no test result files were generated.",
                                            ["testCount"] = testCount,
                                            ["allTestsPassed"] = allSuccess
                                        };
                                    }

                                    completed = true;
                                    break; // Exit the foreach loop
                                }
                            }
                        }
                    }


                    // If completed, break out of the main polling loop
                    if (completed)
                    {
                        break;
                    }

                    // Wait before next attempt
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs = Math.Min(delayMs * 2, maxDelayMs); // Exponential backoff
                }

                // If we completed successfully, return the completion data
                if (completed && completionData != null)
                {
                    // Convert the JObject to a properly formatted response using the smart converter
                    var result = new Dictionary<string, object>();
                    foreach (var property in completionData.Properties())
                    {
                        result[property.Name] = ReadConsoleTool.ConvertJTokenToObjectSmart(property.Value);
                    }
                    return result;
                }

                // Timeout - tool execution failed
                return new
                {
                    success = false,
                    error = "Test runner timed out after waiting for completion. Tests may still be running in Unity. This is a tool execution failure, not a test failure."
                };
            }
            
            // If we get here, Unity didn't return the expected "running" status
            // This might happen if tests complete immediately or if there's an unexpected response format
            _logger.LogWarning("Unexpected response format from Unity. Response: {Response}", response);
            
            // Try to extract any success/failure indication from the response
            if (directSuccess == true)
            {
                return new
                {
                    success = true,
                    message = response.Value<string>("message") ?? "Test execution completed. No test results file was generated.",
                    testCount = 0
                };
            }
            
            return new
            {
                success = false,
                error = $"Unexpected response from Unity. Expected test execution to start. Response: {response}"
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("RunTests operation was cancelled");
            return new
            {
                success = false,
                error = "Operation was cancelled"
            };
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("RunTests operation timed out");
            return new
            {
                success = false,
                error = "Request timed out. Test execution may take a long time. Consider increasing the timeout."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Unity tests");
            return new
            {
                success = false,
                error = $"Failed to run Unity tests: {ex.Message}",
                stackTrace = ex.StackTrace
            };
        }
    }
    
    /// <summary>
    /// Validates if the provided test mode is valid
    /// </summary>
    private static bool IsValidTestMode(string testMode)
    {
        return testMode == "EditMode" || testMode == "PlayMode" || testMode == "All";
    }
    
    /// <summary>
    /// Extracts the test name from a log message
    /// </summary>
    private static string ExtractTestName(string logMessage)
    {
        // Format: "[RunTests] ✓ PASS: TestName (0.123s)" or "[RunTests] ✗ FAIL: TestName (0.123s)"
        var startIndex = logMessage.IndexOf("PASS:") + 5;
        if (startIndex < 5)
        {
            startIndex = logMessage.IndexOf("FAIL:") + 5;
        }
        
        if (startIndex > 5)
        {
            var endIndex = logMessage.IndexOf(" (", startIndex);
            if (endIndex > startIndex)
            {
                return logMessage.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }
        
        return "";
    }
    
    /// <summary>
    /// Extracts the test duration from a log message
    /// </summary>
    private static double ExtractDuration(string logMessage)
    {
        // Format: "(0.123s)"
        var match = System.Text.RegularExpressions.Regex.Match(logMessage, @"\((\d+\.?\d*)s\)");
        if (match.Success && double.TryParse(match.Groups[1].Value, out var duration))
        {
            return duration;
        }
        return 0.0;
    }
    
    /// <summary>
    /// Extracts the assembly name from a fully qualified test name
    /// </summary>
    private static string ExtractAssembly(string fullName)
    {
        var parts = fullName.Split('.');
        return parts.Length > 0 ? parts[0] : "Unknown";
    }
    
    /// <summary>
    /// Extracts the namespace from a fully qualified test name
    /// </summary>
    private static string ExtractNamespace(string fullName)
    {
        var lastDotIndex = fullName.LastIndexOf('.');
        if (lastDotIndex > 0)
        {
            var nameWithoutMethod = fullName.Substring(0, lastDotIndex);
            var secondLastDotIndex = nameWithoutMethod.LastIndexOf('.');
            if (secondLastDotIndex > 0)
            {
                return nameWithoutMethod.Substring(0, secondLastDotIndex);
            }
        }
        return string.Empty;
    }
    
    /// <summary>
    /// Extracts the container script name from a fully qualified test name
    /// </summary>
    private static string ExtractContainerScript(string fullName)
    {
        var parts = fullName.Split('.');
        if (parts.Length >= 2)
        {
            return parts[parts.Length - 2];
        }
        return string.Empty;
    }
}