using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;
using UMCPServer.Services;

namespace UMCPServer.Tools;

/// <summary>
/// Server-side tool for managing Unity Editor operations including play mode control, tool management, 
/// tags, layers, and editor state queries. Provides comprehensive Unity Editor automation through MCP interface.
/// </summary>
[McpServerToolType]
public class ManageEditorTool
{
    private readonly ILogger<ManageEditorTool> _logger;
    private readonly UnityConnectionService _unityConnection;

    public ManageEditorTool(ILogger<ManageEditorTool> logger, UnityConnectionService unityConnection)
    {
        _logger = logger;
        _unityConnection = unityConnection;
    }

    [McpServerTool]
    [Description("Manage Unity Editor operations - control play mode, manage tags/layers, get editor state, and handle tool selection")]
    public async Task<object> ManageEditor(
        [Required]
        [Description("Action to perform: 'play', 'pause', 'stop', 'get_state', 'get_windows', 'get_active_tool', 'get_selection', 'set_active_tool', 'add_tag', 'remove_tag', 'get_tags', 'add_layer', 'remove_layer', 'get_layers'")]
        string action,

        [Description("Tag name for tag operations (required for add_tag, remove_tag)")]
        string? tagName = null,

        [Description("Layer name for layer operations (required for add_layer, remove_layer)")]
        string? layerName = null,

        [Description("Tool name for set_active_tool operation (e.g., 'Move', 'Rotate', 'Scale', 'Rect', 'Transform')")]
        string? toolName = null,

        [Description("Whether to wait for operation completion (optional, default: false)")]
        bool waitForCompletion = false,

        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("ManageEditor called with action: {Action}", action);

            // Validate action parameter
            if (string.IsNullOrWhiteSpace(action))
            {
                return new
                {
                    success = false,
                    error = "Required parameter 'action' is missing or empty."
                };
            }

            action = action.ToLower();

            // Validate action value
            var validActions = new[] { 
                "play", "pause", "stop", "get_state", "get_windows", "get_active_tool", 
                "get_selection", "set_active_tool", "add_tag", "remove_tag", "get_tags", 
                "add_layer", "remove_layer", "get_layers"
            };
            
            if (!validActions.Contains(action))
            {
                return new
                {
                    success = false,
                    error = $"Invalid action '{action}'. Valid actions are: {string.Join(", ", validActions)}"
                };
            }

            // Validate required parameters based on action
            var validationResult = ValidateActionParameters(action, tagName, layerName, toolName);
            if (!validationResult.IsValid)
            {
                return new
                {
                    success = false,
                    error = validationResult.ErrorMessage
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

            // Prepare parameters for Unity
            var parameters = BuildUnityParameters(action, tagName, layerName, toolName, waitForCompletion);

            // Send command to Unity
            var result = await _unityConnection.SendCommandAsync("manage_editor", parameters, cancellationToken);

            if (result == null)
            {
                return new
                {
                    success = false,
                    error = "No response received from Unity within the timeout period"
                };
            }

            // Check if the response indicates success or error
            string? status = result.Value<string?>("status");

            if (status == "error")
            {
                return new
                {
                    success = false,
                    error = result.Value<string?>("error") ?? "Unknown error occurred"
                };
            }

            // Extract the result
            var resultData = result["result"];

            // Handle different action responses
            return action switch
            {
                "play" or "pause" or "stop" => HandlePlayModeResponse(resultData),
                "get_state" => HandleGetStateResponse(resultData),
                "get_windows" => HandleGetWindowsResponse(resultData),
                "get_active_tool" => HandleGetActiveToolResponse(resultData),
                "get_selection" => HandleGetSelectionResponse(resultData),
                "set_active_tool" => HandleSetActiveToolResponse(resultData),
                "add_tag" or "remove_tag" => HandleTagResponse(resultData, action),
                "get_tags" => HandleGetTagsResponse(resultData),
                "add_layer" or "remove_layer" => HandleLayerResponse(resultData, action),
                "get_layers" => HandleGetLayersResponse(resultData),
                _ => new
                {
                    success = true,
                    message = resultData?.ToString() ?? $"Action '{action}' completed",
                    data = resultData
                }
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ManageEditor operation was cancelled");
            return new
            {
                success = false,
                error = "Operation was cancelled"
            };
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("ManageEditor operation timed out");
            return new
            {
                success = false,
                error = "Request timed out. Unity may be busy or unresponsive."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error managing Unity Editor");
            return new
            {
                success = false,
                error = $"Failed to manage Unity Editor: {ex.Message}"
            };
        }
    }

    #region Parameter Validation

    /// <summary>
    /// Validates required parameters based on the action being performed.
    /// </summary>
    private static (bool IsValid, string? ErrorMessage) ValidateActionParameters(
        string action, string? tagName, string? layerName, string? toolName)
    {
        return action switch
        {
            "add_tag" or "remove_tag" => ValidateTagParameters(tagName, action),
            "add_layer" or "remove_layer" => ValidateLayerParameters(layerName, action),
            "set_active_tool" => ValidateToolParameters(toolName),
            _ => (true, null) // Other actions don't require specific parameters
        };
    }

    private static (bool IsValid, string? ErrorMessage) ValidateTagParameters(string? tagName, string action)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return (false, $"'tagName' parameter is required for '{action}' action.");

        return (true, null);
    }

    private static (bool IsValid, string? ErrorMessage) ValidateLayerParameters(string? layerName, string action)
    {
        if (string.IsNullOrWhiteSpace(layerName))
            return (false, $"'layerName' parameter is required for '{action}' action.");

        return (true, null);
    }

    private static (bool IsValid, string? ErrorMessage) ValidateToolParameters(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return (false, "'toolName' parameter is required for 'set_active_tool' action.");

        return (true, null);
    }

    #endregion

    #region Unity Parameter Building

    /// <summary>
    /// Builds the parameter object to send to Unity based on the action and provided parameters.
    /// </summary>
    private static JObject BuildUnityParameters(
        string action, string? tagName, string? layerName, string? toolName, bool waitForCompletion)
    {
        var parameters = new JObject
        {
            ["action"] = action
        };

        // Add tag name if provided
        if (!string.IsNullOrWhiteSpace(tagName))
            parameters["tagName"] = tagName;

        // Add layer name if provided
        if (!string.IsNullOrWhiteSpace(layerName))
            parameters["layerName"] = layerName;

        // Add tool name if provided
        if (!string.IsNullOrWhiteSpace(toolName))
            parameters["toolName"] = toolName;

        // Add wait for completion flag
        if (waitForCompletion)
            parameters["waitForCompletion"] = waitForCompletion;

        return parameters;
    }

    #endregion

    #region Response Handlers

    private static object HandlePlayModeResponse(JToken? resultData)
    {
        return new
        {
            success = true,
            message = resultData?.Value<string?>("message") ?? "Play mode operation completed successfully",
            editorState = resultData
        };
    }

    private static object HandleGetStateResponse(JToken? resultData)
    {
        return new
        {
            success = true,
            message = "Retrieved Unity Editor state successfully",
            editorState = resultData
        };
    }

    private static object HandleGetWindowsResponse(JToken? resultData)
    {
        var windows = resultData?.ToObject<List<object>>() ?? new List<object>();

        return new
        {
            success = true,
            message = $"Found {windows.Count} open editor window(s)",
            windows = windows
        };
    }

    private static object HandleGetActiveToolResponse(JToken? resultData)
    {
        return new
        {
            success = true,
            message = "Retrieved active tool information successfully",
            toolInfo = resultData
        };
    }

    private static object HandleGetSelectionResponse(JToken? resultData)
    {
        return new
        {
            success = true,
            message = "Retrieved current selection successfully",
            selection = resultData
        };
    }

    private static object HandleSetActiveToolResponse(JToken? resultData)
    {
        return new
        {
            success = true,
            message = resultData?.Value<string?>("message") ?? "Active tool set successfully",
            toolInfo = resultData
        };
    }

    private static object HandleTagResponse(JToken? resultData, string action)
    {
        var actionWord = action == "add_tag" ? "added" : "removed";
        return new
        {
            success = true,
            message = resultData?.Value<string?>("message") ?? $"Tag {actionWord} successfully",
            tags = resultData
        };
    }

    private static object HandleGetTagsResponse(JToken? resultData)
    {
        var tags = resultData?.ToObject<List<string>>() ?? new List<string>();

        return new
        {
            success = true,
            message = $"Retrieved {tags.Count} tag(s)",
            tags = tags
        };
    }

    private static object HandleLayerResponse(JToken? resultData, string action)
    {
        var actionWord = action == "add_layer" ? "added" : "removed";
        return new
        {
            success = true,
            message = resultData?.Value<string?>("message") ?? $"Layer {actionWord} successfully",
            layers = resultData
        };
    }

    private static object HandleGetLayersResponse(JToken? resultData)
    {
        return new
        {
            success = true,
            message = "Retrieved current layers successfully",
            layers = resultData
        };
    }

    #endregion
}