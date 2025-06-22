# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

UMCP (Unity Model Context Protocol) is a bridge system that enables MCP clients to interact with the Unity Editor. It consists of:
- **UMCPServer**: A C# .NET 9.0 MCP server that bridges MCP requests to Unity
- **UMCPClient**: A Unity Editor extension that processes commands inside Unity

The system uses TCP communication (ports 6400/6401) to enable AI assistants and other MCP clients to control Unity Editor, manage scenes, read console output, and track development progress.

## Common Development Commands

### UMCPServer (.NET)
```bash
# Build
dotnet build -c Release

# Run tests
dotnet test

# Run integration tests specifically
./UMCPServer/UMCPServer.Tests/RunIntegrationTests.ps1

# Run server locally
dotnet run

# Docker build & run
docker build -t umcpserver .
docker run -it --rm -e UNITY_HOST=host.docker.internal umcpserver
```

### UMCPClient (Unity)
- Open in Unity Editor (Unity 6000.0.32f1 or compatible)
- The client starts automatically when Unity loads
- Access Editor State Monitor: UMCP > Editor State Monitor

## High-Level Architecture

### Communication Flow
1. MCP Client → UMCPServer (port 6500)
2. UMCPServer → Unity TCP Server (port 6400 for commands, 6401 for state)
3. Unity processes command via UMCPBridge
4. Response flows back through the same path

### Key Components

**UMCPServer Structure:**
- `Program.cs`: Entry point, dependency injection setup
- `Services/UnityConnectionService.cs`: Manages TCP connection to Unity
- `Services/UnityStateConnectionService.cs`: Handles real-time Unity state updates
- `Tools/`: MCP tool implementations that forward to Unity
- Version managed in `Directory.Build.props`

**UMCPClient Structure:**
- `Assets/UMCP/Editor/UMCPBridge.cs`: Main TCP server in Unity
- `Assets/UMCP/Editor/Tools/`: Unity-side tool handlers
- `Assets/UMCP/Editor/Helpers/EditorStateHelper.cs`: Tracks Unity Editor state (PlayMode/EditMode, Compiling, etc.)

### Available MCP Tools
1. **GetProjectPath**: Unity project paths
2. **GetServerVersion**: Server version info
3. **GetUnityClientState**: Unity Editor state (runmode/context)
4. **ForceUpdateEditor**: Forces Unity Editor update
5. **ManageScene**: Create/load/save scenes, query hierarchy
6. **ExecuteMenuItem**: Execute Unity menu items
7. **WaitForUnityState**: Wait for specific Unity states
8. **ReadConsole**: Read/clear Unity console logs
9. **MarkStartOfNewStep**: Mark development steps
10. **RequestStepLogs**: Retrieve step-specific logs

### Unity Editor States

**Runmode States:**
- `EditMode_Scene`: Editing scene, can modify files
- `EditMode_Prefab`: Editing prefab, can modify files
- `PlayMode`: Playing, no file modifications

**Context States:**
- `Running`: Normal operation
- `Switching`: Transitioning between modes
- `Compiling`: Scripts compiling
- `UpdatingAssets`: Asset database updating

Always check `EditorStateHelper.CanModifyProjectFiles` before file operations.

## Adding New Tools

1. Create tool class in `UMCPServer/Tools/`:
```csharp
[McpServerToolType]
public class YourTool
{
    private readonly UnityConnectionService _unityConnection;
    
    [McpServerTool]
    [Description("Your description")]
    public async Task<object> YourMethod(string param, CancellationToken ct = default)
    {
        var parameters = new JObject { ["param"] = param };
        return await _unityConnection.SendCommandAsync("your_command", parameters, ct);
    }
}
```

2. Register in `Program.cs`: `.WithTools<YourTool>()`

3. Add Unity handler in `UMCPClient/Assets/UMCP/Editor/Tools/`

## Testing

- Unit tests: Mock dependencies, test components in isolation
- Integration tests: Use IEnumerator pattern for multi-step workflows
- Unity Bridge tests: Real connections to Unity (no mocking)

Filter tests: `dotnet test --filter "Category=Integration"`

## Important Notes

- Auto-reconnection handles Unity restarts
- 16MB buffer size for large responses
- Timeout protection on all operations
- Never modify files during PlayMode or Compiling states
- Use `EditorStateHelper` events for state-aware operations