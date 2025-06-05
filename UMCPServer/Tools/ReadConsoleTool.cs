using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;
using UMCPServer.Services;

namespace UMCPServer.Tools;

[McpServerToolType]
public class ReadConsoleTool
{
    private readonly ILogger<ReadConsoleTool> _logger;
    private readonly UnityConnectionService _unityConnection;
    
    public ReadConsoleTool(ILogger<ReadConsoleTool> logger, UnityConnectionService unityConnection)
    {
        _logger = logger;
        _unityConnection = unityConnection;
    }
    
    [McpServerTool]
    [Description("Read Unity Editor console log entries. Can retrieve specific types of logs, filter by text, and optionally clear the console.")]
    public async Task<object> ReadConsole(
        [Description("Action to perform: 'get' (default) to retrieve logs, or 'clear' to clear the console")]
        string action = "get",
        
        [Description("Types of logs to retrieve (when action is 'get'). Can be: 'error', 'warning', 'log', or 'all'. Default is ['error', 'warning', 'log']")]
        string[]? types = null,
        
        [Description("Maximum number of log entries to return")]
        int? count = null,
        
        [Description("Filter logs by text content (case-insensitive)")]
        string? filterText = null,
        
        [Description("Return format: 'detailed' (default) returns structured data, 'plain' returns just messages")]
        string format = "detailed",
        
        [Description("Include stack traces in the output (default: true)")]
        bool includeStacktrace = true,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Reading Unity console with action: {Action}", action);
            
            if (!_unityConnection.IsConnected && !await _unityConnection.ConnectAsync())
            {
                return new
                {
                    success = false,
                    error = "Unity Editor is not running or MCP Bridge is not available. Please ensure Unity Editor is open and the UMCP Unity3D Client is active."
                };
            }
            
            // Build parameters for the Unity command
            var parameters = new JObject
            {
                ["action"] = action
            };
            
            if (action == "get")
            {
                // Add 'get' specific parameters
                if (types != null && types.Length > 0)
                {
                    parameters["types"] = new JArray(types);
                }
                
                if (count.HasValue)
                {
                    parameters["count"] = count.Value;
                }
                
                if (!string.IsNullOrEmpty(filterText))
                {
                    parameters["filterText"] = filterText;
                }
                
                parameters["format"] = format;
                parameters["includeStacktrace"] = includeStacktrace;
            }
            
            // Send command to Unity
            var response = await _unityConnection.SendCommandAsync("read_console", parameters, cancellationToken);
            
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
            
            // For 'clear' action, return simple success message
            if (action == "clear")
            {
                return new
                {
                    success = true,
                    message = response.Value<string>("message") ?? "Console cleared successfully"
                };
            }
            
            // For 'get' action, return the log entries
            var message = response.Value<string>("message");
            var data = response["data"];
            
            return new
            {
                success = true,
                message = message ?? "Log entries retrieved successfully",
                entries = data,
                count = data?.Count() ?? 0
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ReadConsole operation was cancelled");
            return new
            {
                success = false,
                error = "Operation was cancelled"
            };
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("ReadConsole operation timed out");
            return new
            {
                success = false,
                error = "Request timed out. Unity may be busy or unresponsive."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading Unity console");
            return new
            {
                success = false,
                error = $"Failed to read Unity console: {ex.Message}"
            };
        }
    }
}
