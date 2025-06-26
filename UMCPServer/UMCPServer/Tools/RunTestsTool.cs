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
            
            // Send command to Unity - this will be a long-running operation
            var response = await _unityConnection.SendCommandAsync("run_tests", parameters, cancellationToken);
            
            if (response == null)
            {
                return new
                {
                    success = false,
                    error = "Failed to get response from Unity"
                };
            }
            
            // Extract the response data
            var status = response.Value<string>("status");
            if (status == "error")
            {
                return new
                {
                    success = false,
                    error = response.Value<string>("error") ?? "Unknown error from Unity"
                };
            }
            
            // Extract test results
            var result = response["result"];
            var data = response["data"];
            
            if (data == null || data.Type == JTokenType.Null)
            {
                return new
                {
                    success = false,
                    error = "No test data returned from Unity"
                };
            }
            
            // Build response based on what was requested
            var returnData = new JObject
            {
                ["success"] = true,
                ["AllSuccess"] = data.Value<bool>("AllSuccess")
            };
            
            if (OutputTestResults && data["TestResults"] != null)
            {
                var testResults = data["TestResults"] as JArray;
                List<object> formattedResults = new List<object>();
                
                if (testResults != null)
                {
                    formattedResults = testResults.Select(test => new
                    {
                        TestName = test.Value<string>("TestName"),
                        TestAssembly = test.Value<string>("TestAssembly"),
                        TestNamespace = test.Value<string>("TestNamespace"),
                        ContainerScript = test.Value<string>("ContainerScript"),
                        Success = test.Value<bool>("Success")
                    }).Cast<object>().ToList();
                }
                
                returnData["TestResults"] = JArray.FromObject(formattedResults);
                returnData["TestCount"] = formattedResults.Count();
            }
            
            if (OutputLogData && data["LogData"] != null)
            {
                returnData["LogData"] = data.Value<string>("LogData");
            }
            
            return returnData;
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
                error = $"Failed to run Unity tests: {ex.Message}"
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
}