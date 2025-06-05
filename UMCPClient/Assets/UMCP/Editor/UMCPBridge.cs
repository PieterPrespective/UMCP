using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UMCP.Editor.Models;
using UMCP.Editor.Settings;
using UMCP.Editor.Tools;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace UMCP.Editor
{
    [InitializeOnLoad]
    public static partial class UMCPBridge
    {
        // State change notification
        private static TcpListener stateListener;
        private static readonly List<TcpClient> stateClients = new();

        private static SemaphoreSlim stateSemaphore = new(1, 1); // Semaphore to control
        
        // Main command handling
        private static TcpListener listener;
        private static bool isRunning = false;
        private static readonly object lockObj = new();
        private static Dictionary<string, (string commandJson, TaskCompletionSource<string> tcs)> commandQueue = new();
        
        // Port configuration from settings
        private static int UnityPort => UMCPSettings.Instance.CommandPort;
        private static int StatePort => UMCPSettings.Instance.StatePort;
        private static IPAddress BindAddress => IPAddress.Parse(UMCPSettings.Instance.BindAddress);

        public static bool IsRunning => isRunning;

        public static bool FolderExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (path.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                return true;

            string fullPath = Path.Combine(Application.dataPath, path.StartsWith("Assets/") ? path.Substring(7) : path);
            return Directory.Exists(fullPath);
        }

        static UMCPBridge()
        {
            Start();
            EditorApplication.quitting += Stop;
            
            // Subscribe to state changes
            UMCP.Editor.Helpers.EditorStateHelper.OnRunmodeChanged += OnRunmodeChanged;
            UMCP.Editor.Helpers.EditorStateHelper.OnContextChanged += OnContextChanged;
        }

        public static void Start()
        {
            if (isRunning) return;
            isRunning = true;
            
            // Start main command listener
            listener = new TcpListener(BindAddress, UnityPort);
            listener.Start();
            Debug.Log($"UMCPBridge command listener started on {BindAddress}:{UnityPort}.");
            
            // Start state listener on separate port
            stateListener = new TcpListener(BindAddress, StatePort);
            stateListener.Start();
            Debug.Log($"UMCPBridge state listener started on {BindAddress}:{StatePort}.");
            
            Task.Run(ListenerLoop);
            Task.Run(StateListenerLoop);
            EditorApplication.update += ProcessCommands;
        }

        public static async void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            
            // Stop listeners
            listener?.Stop();
            stateListener?.Stop();
            
            EditorApplication.update -= ProcessCommands;
            
            // Unsubscribe from state changes
            UMCP.Editor.Helpers.EditorStateHelper.OnRunmodeChanged -= OnRunmodeChanged;
            UMCP.Editor.Helpers.EditorStateHelper.OnContextChanged -= OnContextChanged;

            // Close all state client connections

            await stateSemaphore.WaitAsync();
            try
            {
                foreach (var client in stateClients)
                {
                    try
                    {
                        client.Close();
                    }
                    catch { }
                }
                stateClients.Clear();
            }
            finally
            {
                stateSemaphore.Release();
            }

            Debug.Log("UMCPBridge stopped.");
        }

        private static async Task ListenerLoop()
        {
            while (isRunning)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    // Enable basic socket keepalive
                    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                    // Set longer receive timeout to prevent quick disconnections
                    client.ReceiveTimeout = UMCPSettings.Instance.SocketTimeout * 1000; // Convert to milliseconds

                    // Fire and forget each client connection
                    _ = HandleClientAsync(client);
                }
                catch (Exception ex)
                {
                    if (isRunning) Debug.LogError($"Listener error: {ex.Message}");
                }
            }
        }

        private static async Task StateListenerLoop()
        {
            while (isRunning)
            {
                try
                {
                    var client = await stateListener.AcceptTcpClientAsync();
                    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    client.SendTimeout = UMCPSettings.Instance.StateSendTimeout * 1000; // Convert to milliseconds

                    await stateSemaphore.WaitAsync();
                    try
                    {
                        stateClients.Add(client);
                    }
                    finally
                    {
                        stateSemaphore.Release();
                    }

                    
                    Debug.Log($"State client connected from {client.Client.RemoteEndPoint}");
                    
                    // Send initial state immediately
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var initialState = new
                            {
                                type = "state_update",
                                @params = GetCurrentState()
                            };
                            var stateJson = JsonConvert.SerializeObject(initialState);
                            var stateBytes = System.Text.Encoding.UTF8.GetBytes(stateJson);
                            var stream = client.GetStream();
                            await stream.WriteAsync(stateBytes, 0, stateBytes.Length);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error sending initial state: {ex.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (isRunning) Debug.LogError($"State listener error: {ex.Message}");
                }
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            // Add client to connected clients list
            lock (clientsLock)
            {
                connectedClients.Add(client);
            }

            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[8192];
                    while (isRunning)
                    {
                        try
                        {
                            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead == 0) break; // Client disconnected

                            string commandText = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            string commandId = Guid.NewGuid().ToString();
                            var tcs = new TaskCompletionSource<string>();

                            // Special handling for ping command to avoid JSON parsing
                            if (commandText.Trim() == "ping")
                            {
                                // Direct response to ping without going through JSON parsing
                                byte[] pingResponseBytes = System.Text.Encoding.UTF8.GetBytes("{\"status\":\"success\",\"result\":{\"message\":\"pong\"}}");
                                await stream.WriteAsync(pingResponseBytes, 0, pingResponseBytes.Length);
                                continue;
                            }

                            lock (lockObj)
                            {
                                commandQueue[commandId] = (commandText, tcs);
                            }

                            string response = await tcs.Task;
                            byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(response);
                            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Client handler error: {ex.Message}");
                            break;
                        }
                    }
                }
            }
            finally
            {
                // Remove client from connected clients list
                lock (clientsLock)
                {
                    connectedClients.Remove(client);
                }
            }
        }

        private static void ProcessCommands()
        {
            List<string> processedIds = new();
            lock (lockObj)
            {
                foreach (var kvp in commandQueue.ToList())
                {
                    string id = kvp.Key;
                    string commandText = kvp.Value.commandJson;
                    var tcs = kvp.Value.tcs;

                    try
                    {
                        // Special case handling
                        if (string.IsNullOrEmpty(commandText))
                        {
                            var emptyResponse = new
                            {
                                status = "error",
                                error = "Empty command received"
                            };
                            tcs.SetResult(JsonConvert.SerializeObject(emptyResponse));
                            processedIds.Add(id);
                            continue;
                        }

                        // Trim the command text to remove any whitespace
                        commandText = commandText.Trim();

                        // Non-JSON direct commands handling (like ping)
                        if (commandText == "ping")
                        {
                            var pingResponse = new
                            {
                                status = "success",
                                result = new { message = "pong" }
                            };
                            tcs.SetResult(JsonConvert.SerializeObject(pingResponse));
                            processedIds.Add(id);
                            continue;
                        }

                        // Check if the command is valid JSON before attempting to deserialize
                        if (!IsValidJson(commandText))
                        {
                            var invalidJsonResponse = new
                            {
                                status = "error",
                                error = "Invalid JSON format",
                                receivedText = commandText.Length > 50 ? commandText.Substring(0, 50) + "..." : commandText
                            };
                            tcs.SetResult(JsonConvert.SerializeObject(invalidJsonResponse));
                            processedIds.Add(id);
                            continue;
                        }

                        // Normal JSON command processing
                        var command = JsonConvert.DeserializeObject<Command>(commandText);
                        if (command == null)
                        {
                            var nullCommandResponse = new
                            {
                                status = "error",
                                error = "Command deserialized to null",
                                details = "The command was valid JSON but could not be deserialized to a Command object"
                            };
                            tcs.SetResult(JsonConvert.SerializeObject(nullCommandResponse));
                        }
                        else
                        {
                            string responseJson = ExecuteCommand(command);
                            tcs.SetResult(responseJson);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error processing command: {ex.Message}\n{ex.StackTrace}");

                        var response = new
                        {
                            status = "error",
                            error = ex.Message,
                            commandType = "Unknown (error during processing)",
                            receivedText = commandText?.Length > 50 ? commandText.Substring(0, 50) + "..." : commandText
                        };
                        string responseJson = JsonConvert.SerializeObject(response);
                        tcs.SetResult(responseJson);
                    }

                    processedIds.Add(id);
                }

                foreach (var id in processedIds)
                {
                    commandQueue.Remove(id);
                }
            }
        }

        // Helper method to check if a string is valid JSON
        private static bool IsValidJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();
            if ((text.StartsWith("{") && text.EndsWith("}")) || // Object
                (text.StartsWith("[") && text.EndsWith("]")))   // Array
            {
                try
                {
                    JToken.Parse(text);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static string ExecuteCommand(Command command)
        {
            try
            {
                if (string.IsNullOrEmpty(command.type))
                {
                    var errorResponse = new
                    {
                        status = "error",
                        error = "Command type cannot be empty",
                        details = "A valid command type is required for processing"
                    };
                    return JsonConvert.SerializeObject(errorResponse);
                }

                // Handle ping command for connection verification
                if (command.type.Equals("ping", StringComparison.OrdinalIgnoreCase))
                {
                    var pingResponse = new { status = "success", result = new { message = "pong" } };
                    return JsonConvert.SerializeObject(pingResponse);
                }

                // Use JObject for parameters as the new handlers likely expect this
                JObject paramsObject = command.@params ?? new JObject();

                // Route command based on the new tool structure from the refactor plan
                object result = command.type switch
                {
                    // Maps the command type (tool name) to the corresponding handler's static HandleCommand method
                    // Assumes each handler class has a static method named 'HandleCommand' that takes JObject parameters
                    "manage_script" => ManageScript.HandleCommand(paramsObject),
                    "manage_scene" => ManageScene.HandleCommand(paramsObject),
                    "manage_editor" => ManageEditor.HandleCommand(paramsObject),
                    "manage_gameobject" => ManageGameObject.HandleCommand(paramsObject),
                    "manage_asset" => ManageAsset.HandleCommand(paramsObject),
                    "read_console" => ReadConsole.HandleCommand(paramsObject),
                    "execute_menu_item" => ExecuteMenuItem.HandleCommand(paramsObject),
                    "get_project_path" => GetProjectPath.HandleCommand(paramsObject),
                    "get_unity_state" => GetCurrentState(), // New command for getting current state
                    "mark_start_of_new_step" => MarkStartOfNewStep.HandleCommand(paramsObject),
                    "request_step_logs" => RequestStepLogs.HandleCommand(paramsObject),
                    _ => throw new ArgumentException($"Unknown or unsupported command type: {command.type}")
                };


                

                // Standard success response format
                var response = new { status = "success", result };
                Debug.Log($"Command '{command.type}' executed successfully with parameters: {JsonConvert.SerializeObject(response)}");


                return JsonConvert.SerializeObject(response);
            }
            catch (Exception ex)
            {
                // Log the detailed error in Unity for debugging
                Debug.LogError($"Error executing command '{command?.type ?? "Unknown"}': {ex.Message}\n{ex.StackTrace}");

                // Standard error response format
                var response = new
                {
                    status = "error",
                    error = ex.Message, // Provide the specific error message
                    command = command?.type ?? "Unknown", // Include the command type if available
                    stackTrace = ex.StackTrace, // Include stack trace for detailed debugging
                    paramsSummary = command?.@params != null ? GetParamsSummary(command.@params) : "No parameters" // Summarize parameters for context
                };
                return JsonConvert.SerializeObject(response);
            }
        }

        // Helper method to get a summary of parameters for error reporting
        private static string GetParamsSummary(JObject @params)
        {
            try
            {
                if (@params == null || !@params.HasValues)
                    return "No parameters";

                return string.Join(", ", @params.Properties().Select(p => $"{p.Name}: {p.Value?.ToString()?.Substring(0, Math.Min(20, p.Value?.ToString()?.Length ?? 0))}"));
            }
            catch
            {
                return "Could not summarize parameters";
            }
        }

        #region State Change Reporting

        private static void OnRunmodeChanged(UMCP.Editor.Helpers.EditorStateHelper.Runmode previousRunmode, UMCP.Editor.Helpers.EditorStateHelper.Runmode newRunmode)
        {
            ReportStateChange("runmode", previousRunmode.ToString(), newRunmode.ToString());
        }

        private static void OnContextChanged(UMCP.Editor.Helpers.EditorStateHelper.Context previousContext, UMCP.Editor.Helpers.EditorStateHelper.Context newContext)
        {
            ReportStateChange("context", previousContext.ToString(), newContext.ToString());
        }

        private static void ReportStateChange(string stateType, string previousValue, string newValue)
        {
            Task.Run(async () =>
            {
                try
                {
                    var stateChange = new
                    {
                        type = "state_change",
                        @params = new JObject
                        {
                            ["stateType"] = stateType,
                            ["previousValue"] = previousValue,
                            ["newValue"] = newValue,
                            ["timestamp"] = DateTime.UtcNow.ToString("o"),
                            ["currentRunmode"] = UMCP.Editor.Helpers.EditorStateHelper.CurrentRunmode.ToString(),
                            ["currentContext"] = UMCP.Editor.Helpers.EditorStateHelper.CurrentContext.ToString()
                        }
                    };

                    string stateJson = JsonConvert.SerializeObject(stateChange);
                    
                    // Send to all connected state clients (on the separate port)
                    await SendStateToStateClients(stateJson);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error reporting state change: {ex.Message}");
                }
            });
        }

        // Keep track of all connected clients to send state updates
        private static readonly List<TcpClient> connectedClients = new();
        private static readonly object clientsLock = new();

        private static async Task SendStateToStateClients(string stateJson)
        {
            byte[] stateBytes = System.Text.Encoding.UTF8.GetBytes(stateJson);
            List<TcpClient> disconnectedClients = new();

            await stateSemaphore.WaitAsync();
            try
            {
                foreach (var client in stateClients.ToList())
                {
                    try
                    {
                        if (client.Connected)
                        {
                            var stream = client.GetStream();
                            await stream.WriteAsync(stateBytes, 0, stateBytes.Length);
                            Debug.Log($"State update sent to {client.Client.RemoteEndPoint}");
                        }
                        else
                        {
                            disconnectedClients.Add(client);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error sending state to client: {ex.Message}");
                        disconnectedClients.Add(client);
                    }
                }

                // Remove disconnected clients
                foreach (var client in disconnectedClients)
                {
                    stateClients.Remove(client);
                    client.Close();
                }
            }
            finally
            {
                stateSemaphore.Release();
            }
        }

        // Method to get current state (for the new MCP tool)
        public static JObject GetCurrentState()
        {
            return new JObject
            {
                ["runmode"] = UMCP.Editor.Helpers.EditorStateHelper.CurrentRunmode.ToString(),
                ["context"] = UMCP.Editor.Helpers.EditorStateHelper.CurrentContext.ToString(),
                ["canModifyProjectFiles"] = UMCP.Editor.Helpers.EditorStateHelper.CanModifyProjectFiles,
                ["isEditorResponsive"] = UMCP.Editor.Helpers.EditorStateHelper.IsEditorResponsive,
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        #endregion
    }
}