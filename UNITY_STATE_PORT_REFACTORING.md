# Unity State Port Refactoring

## Overview

This refactoring separates Unity state data (Runmode and Context) communication from the main command channel by introducing a dedicated TCP port for state updates. This improves the architecture by:

1. Separating concerns between command execution and state monitoring
2. Allowing independent connection management for state updates
3. Reducing potential interference between command responses and state changes
4. Enabling more efficient state change notifications

## Changes Made

### Unity Client (UMCP Unity3D Client)

#### 1. Added Configurable Settings System
- **New File**: `Assets/UMCP/Editor/Settings/UMCPSettings.cs`
  - ScriptableObject for persistent configuration
  - Configurable ports for command (default: 6400) and state (default: 6401)
  - Configurable timeouts and network settings
  - Accessible via menu: `UMCP/Settings`

#### 2. Updated UMCPBridge.cs
- Added separate TCP listener for state updates on port 6401
- State updates are now sent only to clients connected to the state port
- Command connections no longer receive state change notifications
- Both listeners can be configured via UMCPSettings

### MCP Server (UMCP MCP Server)

#### 1. Updated ServerConfiguration
- Added `UnityStatePort` property (default: 6401)
- Configurable via `UNITY_STATE_PORT` environment variable

#### 2. New UnityStateConnectionService
- **New File**: `Services/UnityStateConnectionService.cs`
  - Dedicated service for handling state connections
  - Maintains persistent connection to Unity state port
  - Processes state update and state change messages
  - Fires events when state changes occur

#### 3. Updated UnityConnectionService
- Removed all state-related functionality
- Now focuses solely on command execution
- Cleaner separation of concerns

#### 4. Updated Program.cs
- Registers both UnityConnectionService and UnityStateConnectionService
- UnityConnectionLifecycleService now manages both connections
- Attempts to connect to both ports on startup

#### 5. Updated GetUnityClientStateTool
- Now uses UnityStateConnectionService for cached state
- Falls back to command connection if state connection unavailable
- Provides seamless experience regardless of connection status

### Tests

#### Added UnityStateConnectionServiceTests
- **New File**: `UMCPServer.Tests/IntegrationTests/UnityStateConnectionServiceTests.cs`
  - Tests connection establishment
  - Tests initial state reception
  - Tests state change notifications
  - Tests connection failure scenarios

## Configuration

### Unity Client Configuration

1. Open Unity Editor
2. Go to menu: `UMCP/Settings`
3. Configure ports and network settings in the Inspector
4. Settings are saved in `Assets/UMCP/Editor/Settings/UMCPSettings.asset`

### MCP Server Configuration

Set environment variables:
```bash
# Main command port (default: 6400)
UNITY_PORT=6400

# State update port (default: 6401)
UNITY_STATE_PORT=6401

# Unity host (default: localhost, or host.docker.internal in Docker)
UNITY_HOST=localhost
```

### Docker Configuration

When running in Docker, ensure both ports are accessible:
```bash
docker run -e UNITY_HOST=host.docker.internal \
           -e UNITY_PORT=6400 \
           -e UNITY_STATE_PORT=6401 \
           your-umcp-server-image
```

## Benefits

1. **Separation of Concerns**: State updates don't interfere with command execution
2. **Performance**: State changes are pushed immediately without polling
3. **Reliability**: Either connection can fail independently without affecting the other
4. **Scalability**: Different components can connect to different ports based on needs

## Migration Notes

- The main command port (6400) remains unchanged for backward compatibility
- State updates are no longer sent over the command connection
- Clients interested in state updates must connect to the state port (6401)
- The GetUnityClientState tool works with both old and new configurations

## Testing

Run the integration tests to verify the new functionality:
```bash
dotnet test UMCPServer.Tests --filter "FullyQualifiedName~UnityStateConnectionServiceTests"
```

## Future Enhancements

1. WebSocket support for state updates
2. State update filtering/subscription model
3. Historical state tracking
4. State change replay functionality
