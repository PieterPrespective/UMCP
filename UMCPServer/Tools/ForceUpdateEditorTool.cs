using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;
using UMCPServer.Services;

namespace UMCPServer.Tools;

[McpServerToolType]
public class ForceUpdateEditorTool
{
    private readonly ILogger<ForceUpdateEditorTool> _logger;
    private readonly UnityConnectionService _unityConnection;
    private readonly UnityStateConnectionService _stateConnection;
    
    public ForceUpdateEditorTool(
        ILogger<ForceUpdateEditorTool> logger, 
        UnityConnectionService unityConnection,
        UnityStateConnectionService stateConnection)
    {
        _logger = logger;
        _unityConnection = unityConnection;
        _stateConnection = stateConnection;
    }
    
    [McpServerTool]
    [Description("Forces the Unity Editor to update regardless of whether the application has focus. If in PlayMode, reverts to EditMode first. Waits for Unity to reach EditMode_Running state or times out after 30 seconds.")]
    public async Task<object> ForceUpdateEditor(
        [Description("Timeout in milliseconds to wait for Unity to reach EditMode_Running state. Default is 30000 (30 seconds).")]
        int timeoutMilliseconds = 30000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting ForceUpdateEditor with timeout: {Timeout}ms", timeoutMilliseconds);
            
            // Check if Unity connection is available
            if (!_unityConnection.IsConnected && !await _unityConnection.ConnectAsync())
            {
                return new
                {
                    success = false,
                    error = "Unity Editor is not running or MCP Bridge is not available. Please ensure Unity Editor is open and the UMCP Unity3D Client is active."
                };
            }

            // Connect to Unity state service if not already connected
            if (!_stateConnection.IsConnected && !await _stateConnection.ConnectAsync())
            {
                return new
                {
                    success = false,
                    error = "Unable to connect to Unity state monitoring. State monitoring is required for ForceUpdateEditor."
                };
            }

            // Get initial state
            var initialState = _stateConnection.CurrentUnityState;
            string? initialRunmode = initialState?.Value<string>("runmode");
            string? initialContext = initialState?.Value<string>("context");
            
            _logger.LogInformation("Initial Unity state: runmode={Runmode}, context={Context}", 
                initialRunmode ?? "unknown", initialContext ?? "unknown");
            
            // Send the force update command to Unity
            var result = await _unityConnection.SendCommandAsync("force_update_editor", null, cancellationToken);
            
            if (result == null)
            {
                return new
                {
                    success = false,
                    error = "No response received from Unity within the timeout period"
                };
            }
            
            // Check if the command was successful
            bool commandSuccess = result.Value<bool?>("success") ?? false;
            if (!commandSuccess)
            {
                return new
                {
                    success = false,
                    error = result.Value<string?>("error") ?? "Failed to execute force update command"
                };
            }
            
            // Get the initial action that was taken
            var commandResult = result["data"];
            string? action = commandResult?.Value<string>("action");
            
            _logger.LogInformation("Force update command executed successfully. Action: {Action}", action ?? "unknown");
            
            // Now wait for Unity to reach EditMode_Running state
            var startTime = DateTime.UtcNow;
            
            // Create timeout cancellation token
            using var timeoutCts = new CancellationTokenSource(timeoutMilliseconds);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            var tcs = new TaskCompletionSource<JObject>();
            
            // Subscribe to state changes
            void OnStateChanged(JObject newState)
            {
                string? runmode = newState.Value<string>("runmode");
                string? context = newState.Value<string>("context");
                
                _logger.LogDebug("State change received: runmode={Runmode}, context={Context}", runmode, context);
                
                // Check if we've reached the desired state (EditMode with Running context)
                if (IsEditModeRunning(runmode, context))
                {
                    _logger.LogInformation("Unity reached EditMode_Running state");
                    tcs.TrySetResult(newState);
                }
            }
            
            _stateConnection.UnityStateChanged += OnStateChanged;
            
            try
            {
                // Check if we're already in the desired state
                var currentState = _stateConnection.CurrentUnityState;
                if (currentState != null && IsEditModeRunning(
                    currentState.Value<string>("runmode"), 
                    currentState.Value<string>("context")))
                {
                    var waitTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    _logger.LogInformation("Unity is already in EditMode_Running state");
                    
                    return new
                    {
                        success = true,
                        message = "Unity Editor force update completed successfully",
                        initialState = new
                        {
                            runmode = initialRunmode,
                            context = initialContext
                        },
                        finalState = new
                        {
                            runmode = currentState.Value<string>("runmode"),
                            context = currentState.Value<string>("context"),
                            timestamp = currentState.Value<string>("timestamp")
                        },
                        action = action,
                        waitTimeMs = (int)waitTime
                    };
                }
                
                // Wait for the desired state or timeout
                var completedTask = await Task.WhenAny(
                    tcs.Task, 
                    Task.Delay(Timeout.Infinite, linkedCts.Token));
                
                if (completedTask == tcs.Task)
                {
                    var finalState = await tcs.Task;
                    var waitTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    
                    _logger.LogInformation("Unity reached EditMode_Running state after {WaitTime}ms", waitTime);
                    
                    return new
                    {
                        success = true,
                        message = $"Unity Editor force update completed after {waitTime:F0}ms",
                        initialState = new
                        {
                            runmode = initialRunmode,
                            context = initialContext
                        },
                        finalState = new
                        {
                            runmode = finalState.Value<string>("runmode"),
                            context = finalState.Value<string>("context"),
                            timestamp = finalState.Value<string>("timestamp")
                        },
                        action = action,
                        waitTimeMs = (int)waitTime
                    };
                }
                
                // Timeout occurred
                var timeoutState = _stateConnection.CurrentUnityState;
                return new
                {
                    success = false,
                    error = $"Timeout waiting for Unity to reach EditMode_Running state after {timeoutMilliseconds}ms",
                    initialState = new
                    {
                        runmode = initialRunmode,
                        context = initialContext
                    },
                    currentState = new
                    {
                        runmode = timeoutState?.Value<string>("runmode"),
                        context = timeoutState?.Value<string>("context"),
                        timestamp = timeoutState?.Value<string>("timestamp")
                    },
                    action = action,
                    timeoutMs = timeoutMilliseconds
                };
            }
            finally
            {
                _stateConnection.UnityStateChanged -= OnStateChanged;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ForceUpdateEditor operation was cancelled");
            return new
            {
                success = false,
                error = "Operation was cancelled"
            };
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("ForceUpdateEditor operation timed out");
            return new
            {
                success = false,
                error = "Request timed out. Unity may be busy or unresponsive."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ForceUpdateEditor");
            return new
            {
                success = false,
                error = $"Failed to force update Unity Editor: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// Checks if Unity is in EditMode with Running context
    /// </summary>
    private static bool IsEditModeRunning(string? runmode, string? context)
    {
        return (runmode == "EditMode_Scene" || runmode == "EditMode_Prefab") && 
               context == "Running";
    }
}
