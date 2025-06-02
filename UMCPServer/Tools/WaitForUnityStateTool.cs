using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;
using UMCPServer.Services;

namespace UMCPServer.Tools;

[McpServerToolType]
public class WaitForUnityStateTool
{
    private readonly ILogger<WaitForUnityStateTool> _logger;
    private readonly UnityConnectionService _unityConnection;
    
    public WaitForUnityStateTool(ILogger<WaitForUnityStateTool> logger, UnityConnectionService unityConnection)
    {
        _logger = logger;
        _unityConnection = unityConnection;
    }
    
    [McpServerTool]
    [Description("Waits until the Unity3D client has reached the requested state, or the given timeout is exceeded. Returns success when the state is reached or an error if timeout occurs.")]
    public async Task<object> WaitForUnityState(
        [Description("The desired runmode state to wait for (EditMode_Scene, EditMode_Prefab, PlayMode). Optional - if not specified, only context will be checked.")]
        string? targetRunmode = null,
        [Description("The desired context state to wait for (Running, Switching, Compiling, UpdatingAssets). Optional - if not specified, only runmode will be checked.")]
        string? targetContext = null,
        [Description("Timeout in milliseconds to wait for the state. Default is 30000 (30 seconds).")]
        int timeoutMilliseconds = 30000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Waiting for Unity state - Runmode: {Runmode}, Context: {Context}, Timeout: {Timeout}ms", 
                targetRunmode ?? "any", targetContext ?? "any", timeoutMilliseconds);
            
            // Validate parameters
            if (string.IsNullOrEmpty(targetRunmode) && string.IsNullOrEmpty(targetContext))
            {
                return new
                {
                    success = false,
                    error = "At least one of targetRunmode or targetContext must be specified"
                };
            }
            
            // Check if Unity connection is available
            if (!_unityConnection.IsConnected && !await _unityConnection.ConnectAsync())
            {
                return new
                {
                    success = false,
                    error = "Unity Editor is not running or MCP Bridge is not available. Please ensure Unity Editor is open and the UMCP Unity3D Client is active."
                };
            }
            
            // Create timeout cancellation token
            using var timeoutCts = new CancellationTokenSource(timeoutMilliseconds);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            var tcs = new TaskCompletionSource<JObject>();
            
            // Subscribe to state changes
            void OnStateChanged(JObject newState)
            {
                bool runmodeMatches = string.IsNullOrEmpty(targetRunmode) || 
                    newState.Value<string>("runmode")?.Equals(targetRunmode, StringComparison.OrdinalIgnoreCase) == true;
                    
                bool contextMatches = string.IsNullOrEmpty(targetContext) || 
                    newState.Value<string>("context")?.Equals(targetContext, StringComparison.OrdinalIgnoreCase) == true;
                
                if (runmodeMatches && contextMatches)
                {
                    tcs.TrySetResult(newState);
                }
            }
            
            _unityConnection.UnityStateChanged += OnStateChanged;
            
            try
            {
                // Check current state first
                var currentState = _unityConnection.CurrentUnityState;
                if (currentState != null)
                {
                    bool currentRunmodeMatches = string.IsNullOrEmpty(targetRunmode) || 
                        currentState.Value<string>("runmode")?.Equals(targetRunmode, StringComparison.OrdinalIgnoreCase) == true;
                        
                    bool currentContextMatches = string.IsNullOrEmpty(targetContext) || 
                        currentState.Value<string>("context")?.Equals(targetContext, StringComparison.OrdinalIgnoreCase) == true;
                    
                    if (currentRunmodeMatches && currentContextMatches)
                    {
                        _logger.LogInformation("Unity is already in the desired state");
                        return new
                        {
                            success = true,
                            message = "Unity is already in the desired state",
                            runmode = currentState.Value<string>("runmode"),
                            context = currentState.Value<string>("context"),
                            timestamp = currentState.Value<string>("timestamp"),
                            waitTimeMs = 0
                        };
                    }
                }
                
                // Wait for state change
                var startTime = DateTime.UtcNow;
                
                // Force a state refresh to ensure we have the latest
                await _unityConnection.RefreshUnityState();
                
                // Wait for the desired state or timeout
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, linkedCts.Token));
                
                if (completedTask == tcs.Task)
                {
                    var finalState = await tcs.Task;
                    var waitTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    
                    _logger.LogInformation("Unity reached desired state after {WaitTime}ms", waitTime);
                    
                    return new
                    {
                        success = true,
                        message = $"Unity reached the desired state after {waitTime:F0}ms",
                        runmode = finalState.Value<string>("runmode"),
                        context = finalState.Value<string>("context"),
                        timestamp = finalState.Value<string>("timestamp"),
                        waitTimeMs = (int)waitTime
                    };
                }
                
                // Timeout occurred
                var timeoutState = _unityConnection.CurrentUnityState;
                return new
                {
                    success = false,
                    error = $"Timeout waiting for Unity state after {timeoutMilliseconds}ms",
                    currentRunmode = timeoutState?.Value<string>("runmode"),
                    currentContext = timeoutState?.Value<string>("context"),
                    targetRunmode = targetRunmode,
                    targetContext = targetContext
                };
            }
            finally
            {
                _unityConnection.UnityStateChanged -= OnStateChanged;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("WaitForUnityState operation was cancelled");
            return new
            {
                success = false,
                error = "Operation was cancelled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiting for Unity state");
            return new
            {
                success = false,
                error = $"Failed to wait for Unity state: {ex.Message}"
            };
        }
    }
}
