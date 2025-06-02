using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;
using UMCPServer.Services;

namespace UMCPServer.Tools;

[McpServerToolType]
public class GetUnityClientStateTool
{
    private readonly ILogger<GetUnityClientStateTool> _logger;
    private readonly UnityConnectionService _unityConnection;
    
    public GetUnityClientStateTool(ILogger<GetUnityClientStateTool> logger, UnityConnectionService unityConnection)
    {
        _logger = logger;
        _unityConnection = unityConnection;
    }
    
    [McpServerTool]
    [Description("Returns the current state of the connected Unity3D Client including runmode (EditMode_Scene, EditMode_Prefab, PlayMode) and context (Running, Switching, Compiling, UpdatingAssets).")]
    public async Task<object> GetUnityClientState(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting Unity client state");
            
            // Check if Unity connection is available
            if (!_unityConnection.IsConnected && !await _unityConnection.ConnectAsync())
            {
                return new
                {
                    success = false,
                    error = "Unity Editor is not running or MCP Bridge is not available. Please ensure Unity Editor is open and the UMCP Unity3D Client is active."
                };
            }
            
            // Try to get current state from cache first
            var cachedState = _unityConnection.CurrentUnityState;
            if (cachedState != null)
            {
                _logger.LogInformation("Returning cached Unity state");
                return new
                {
                    success = true,
                    message = "Unity client state retrieved successfully",
                    runmode = cachedState.Value<string>("runmode"),
                    context = cachedState.Value<string>("context"),
                    canModifyProjectFiles = cachedState.Value<bool?>("canModifyProjectFiles"),
                    isEditorResponsive = cachedState.Value<bool?>("isEditorResponsive"),
                    timestamp = cachedState.Value<string>("timestamp"),
                    lastChange = cachedState["lastChange"]
                };
            }
            
            // If no cached state, refresh it
            await _unityConnection.RefreshUnityState();
            cachedState = _unityConnection.CurrentUnityState;
            
            if (cachedState != null)
            {
                return new
                {
                    success = true,
                    message = "Unity client state retrieved successfully",
                    runmode = cachedState.Value<string>("runmode"),
                    context = cachedState.Value<string>("context"),
                    canModifyProjectFiles = cachedState.Value<bool?>("canModifyProjectFiles"),
                    isEditorResponsive = cachedState.Value<bool?>("isEditorResponsive"),
                    timestamp = cachedState.Value<string>("timestamp"),
                    lastChange = cachedState["lastChange"]
                };
            }
            
            return new
            {
                success = false,
                error = "Failed to retrieve Unity client state"
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetUnityClientState operation was cancelled");
            return new
            {
                success = false,
                error = "Operation was cancelled"
            };
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("GetUnityClientState operation timed out");
            return new
            {
                success = false,
                error = "Request timed out. Unity may be busy or unresponsive."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Unity client state");
            return new
            {
                success = false,
                error = $"Failed to get Unity client state: {ex.Message}"
            };
        }
    }
}
