# UMCP Console Tools Documentation

This document describes the Unity Console tools implemented in the UMCP MCP Server and Unity Client.

## Overview

The console tools provide functionality to interact with the Unity Editor's Debug Console, including reading logs, clearing the console, and tracking development steps.

## Available Tools

### 1. ReadConsole

Reads Unity Editor console log entries with filtering capabilities.

#### Parameters:
- `action` (string): Action to perform - "get" (default) or "clear"
- `types` (string[]): Types of logs to retrieve - ["error", "warning", "log", "all"]
- `count` (int?): Maximum number of entries to return
- `filterText` (string?): Filter logs by text content (case-insensitive)
- `format` (string): Return format - "detailed" (default) or "plain"
- `includeStacktrace` (bool): Include stack traces (default: true)

#### Example Usage:
```csharp
// Get all error logs
ReadConsole(action: "get", types: new[] { "error" })

// Get last 10 logs containing "GameObject"
ReadConsole(action: "get", count: 10, filterText: "GameObject")

// Clear the console
ReadConsole(action: "clear")
```

### 2. MarkStartOfNewStep

Marks the start of a new development step by creating a special log entry in the Unity Debug Console.

#### Parameters:
- `stepName` (string): The name of the development step to mark

#### Example Usage:
```csharp
MarkStartOfNewStep(stepName: "PlayerControllerImplementation")
```

This creates a specially formatted log entry:
```
[UMCP_STEP_START] Step: 'PlayerControllerImplementation' | Started at: 2024-01-15 10:30:45.123 [/UMCP_STEP_START]
=== Development Step Started: PlayerControllerImplementation ===
```

### 3. RequestStepLogs

Retrieves all log messages that occurred after a specific step was marked.

#### Parameters:
- `stepName` (string): The name of the step to retrieve logs for
- `includeStacktrace` (bool): Include stack traces (default: true)
- `format` (string): Return format - "detailed" (default) or "plain"

#### Example Usage:
```csharp
// Get all logs since marking "PlayerControllerImplementation" step
RequestStepLogs(stepName: "PlayerControllerImplementation")

// Get logs without stack traces in plain format
RequestStepLogs(stepName: "PlayerControllerImplementation", includeStacktrace: false, format: "plain")
```

## Implementation Details

### Unity Client Side

The Unity Client implements these tools in the following files:
- `ReadConsole.cs`: Handles reading and clearing console logs using Unity's internal LogEntries API via reflection
- `MarkStartOfNewStep.cs`: Creates specially formatted log markers for step tracking
- `RequestStepLogs.cs`: Retrieves logs between step markers

### MCP Server Side

The MCP Server exposes these Unity tools through:
- `ReadConsoleTool.cs`: MCP tool wrapper for ReadConsole functionality
- `MarkStartOfNewStepTool.cs`: MCP tool wrapper for marking step starts
- `RequestStepLogsTool.cs`: MCP tool wrapper for requesting step logs

### Communication Flow

1. MCP Server receives tool invocation from client
2. Server connects to Unity via TCP (default port 5001)
3. Server sends command to Unity Client's UMCPBridge
4. Unity Client executes the tool and returns results
5. Server formats and returns the response to the MCP client

## Use Cases

### 1. Debugging Session Tracking
```csharp
// Mark the start of a debugging session
MarkStartOfNewStep("DebugPlayerMovement")

// ... perform debugging actions in Unity ...

// Retrieve all logs from this debugging session
var logs = RequestStepLogs("DebugPlayerMovement")
```

### 2. Feature Implementation Logging
```csharp
// Mark the start of implementing a new feature
MarkStartOfNewStep("ImplementInventorySystem")

// ... implement the feature ...

// Get all logs related to this implementation
var logs = RequestStepLogs("ImplementInventorySystem", format: "detailed")
```

### 3. Error Investigation
```csharp
// Clear console before reproducing an issue
ReadConsole(action: "clear")

// Mark the reproduction attempt
MarkStartOfNewStep("ReproduceSaveLoadBug")

// ... reproduce the bug ...

// Get only error logs from this attempt
var errors = RequestStepLogs("ReproduceSaveLoadBug")
// Or get errors directly without step tracking
var allErrors = ReadConsole(types: new[] { "error" })
```

## Error Handling

All tools return a standardized response format:
```json
{
  "success": true/false,
  "message": "Description of the result",
  "error": "Error message if success is false",
  "data": {...} // Tool-specific data
}
```

Common error scenarios:
- Unity Editor not running
- MCP Bridge not available
- Invalid parameters
- Step name not found (for RequestStepLogs)
- Connection timeout

## Notes

- Step markers are preserved in the Unity console until manually cleared
- The ReadConsole tool uses reflection to access Unity's internal LogEntries API
- Stack traces are extracted from log messages when available
- Log filtering is case-insensitive
- The tools respect Unity's console log limits
