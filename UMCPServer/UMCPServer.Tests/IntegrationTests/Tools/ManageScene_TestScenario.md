# ManageScene Tool Test Scenarios

## Overview
The ManageScene tool provides comprehensive scene management capabilities in Unity, including creating, loading, saving, and querying scene hierarchies. This document outlines the test scenarios for the integration test.

## Test Scenario: Create, Save, Load, and Modify Scene

### Prerequisites
- Unity Editor must be running with the UMCP Unity3D Client active
- The UMCP MCP Server must be able to connect to Unity on port 6400 (default)
- Unity project must have a valid folder structure for saving scenes

### Test Steps

#### 1. **Check Unity Connection**
- Verify that Unity is running with UMCP Client
- Attempt to connect to Unity on the configured port
- Skip test if Unity is not available

#### 2. **Create New Scene**
- **Action**: `create`
- **Parameters**: 
  - `name`: "TestIntegrationScene"
  - `path`: "Scenes/IntegrationTests"
- **Expected Result**: 
  - Scene is created successfully
  - Scene file is saved at `Assets/Scenes/IntegrationTests/TestIntegrationScene.unity`
  - The new scene becomes the active scene

#### 3. **Save Scene**
- **Action**: `save`
- **Parameters**: None (saves current active scene)
- **Expected Result**: 
  - Scene is saved successfully
  - Confirmation that the scene was written to disk

#### 4. **Load Scene**
- **Action**: `load`
- **Parameters**: 
  - `name`: "TestIntegrationScene"
  - `path`: "Scenes/IntegrationTests/TestIntegrationScene.unity"
- **Expected Result**: 
  - Scene is loaded successfully
  - The loaded scene becomes the active scene

#### 5. **Create GameObjects**
- Use ExecuteMenuItem tool to create 3 cubes:
  - Execute menu item "GameObject/3D Object/Cube" three times
  - Small delay between creations to ensure unique naming
- **Expected Result**: 
  - Three cube GameObjects are created
  - They are named: "Cube", "Cube (1)", "Cube (2)"

#### 6. **Get Scene Hierarchy**
- **Action**: `get_hierarchy`
- **Parameters**: None
- **Expected Result**: 
  - Returns complete hierarchy of the active scene
  - Contains all three cube GameObjects with correct names
  - Each GameObject includes transform data and children array

#### 7. **Get Active Scene Info**
- **Action**: `get_active`
- **Parameters**: None
- **Expected Result**: 
  - Returns information about the currently active scene
  - Includes: name, path, isDirty flag, rootCount

### Verification Points

1. **Scene Creation**
   - Verify the scene file exists at the specified path
   - Verify the scene can be loaded after creation

2. **GameObject Creation**
   - Verify exactly 3 cubes are created
   - Verify they have the expected names following Unity's naming convention

3. **Hierarchy Structure**
   - Verify the hierarchy contains all expected GameObjects
   - Verify the data structure includes all required fields

### Cleanup Considerations

- The test creates a scene file that persists in the Unity project
- Consider implementing a cleanup step to delete test scenes after successful completion
- Alternatively, use a dedicated test folder that can be periodically cleaned

### Error Scenarios

The test should handle these potential error conditions:
- Unity not running or UMCP Client not active
- Scene already exists at the target path
- Invalid scene name or path
- Unity compilation errors preventing menu item execution
- Timeout during long operations

### Notes

- Scene operations in Unity can trigger asset database refreshes which may cause delays
- The test uses a coroutine-based approach to handle asynchronous operations
- Each step logs detailed information for debugging purposes
