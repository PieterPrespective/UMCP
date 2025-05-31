# Unity Editor State Helper

## Overview

The EditorStateHelper is a robust Unity Editor utility that tracks and reports the current state of the Unity Editor. It distinguishes between two state machines:

### Runmode States
- **EditMode_Scene**: Editor is in edit mode, editing a scene. Project files can be modified.
- **EditMode_Prefab**: Editor is editing a prefab in isolation. Project files can be modified.
- **PlayMode**: Editor is in play mode. No project file modifications should be made.

### Context States
- **Running**: Normal operation, actions appropriate to the current runmode can be taken.
- **Switching**: Currently transitioning between scenes, play/edit modes, or scene/prefab modes.
- **Compiling**: Scripts are compiling or domain reload is occurring. Editor may be unresponsive.
- **UpdatingAssets**: Asset database is updating (importing, refreshing, or processing assets). Asset operations may be unreliable.

## Features

- **Persistent State Storage**: Uses ScriptableObject with HideFlags to maintain state across domain reloads
- **Real-time State Detection**: Continuously monitors Unity Editor state changes
- **Event System**: Provides events for state changes (OnRunmodeChanged, OnContextChanged, OnStateChanged)
- **Safety Checks**: Properties like `CanModifyProjectFiles` and `IsEditorResponsive` for safe operations
- **Comprehensive Tracking**: Monitors play mode changes, scene switches, prefab editing, compilation, and all forms of asset imports/updates

## Usage

### Basic State Checking

```csharp
using UMCP.Editor.Helpers;

// Check if it's safe to modify files
if (EditorStateHelper.CanModifyProjectFiles)
{
    // Perform file modifications
    AssetDatabase.CreateAsset(myAsset, "Assets/MyAsset.asset");
}

// Check if editor is responsive
if (EditorStateHelper.IsEditorResponsive)
{
    // Perform operations that require responsive editor
    EditorUtility.DisplayProgressBar("Processing", "Working...", 0.5f);
}

// Get current states
var runmode = EditorStateHelper.CurrentRunmode;
var context = EditorStateHelper.CurrentContext;
```

### Event Subscription

```csharp
void OnEnable()
{
    EditorStateHelper.OnRunmodeChanged += HandleRunmodeChange;
    EditorStateHelper.OnContextChanged += HandleContextChange;
}

void OnDisable()
{
    EditorStateHelper.OnRunmodeChanged -= HandleRunmodeChange;
    EditorStateHelper.OnContextChanged -= HandleContextChange;
}

void HandleRunmodeChange(EditorStateHelper.Runmode previous, EditorStateHelper.Runmode current)
{
    Debug.Log($"Runmode changed from {previous} to {current}");
}

void HandleContextChange(EditorStateHelper.Context previous, EditorStateHelper.Context current)
{
    Debug.Log($"Context changed from {previous} to {current}");
}
```

### Safe Operations Example

```csharp
public static void SafeAssetOperation()
{
    // Wait for safe context
    if (EditorStateHelper.CurrentContext != EditorStateHelper.Context.Running)
    {
        EditorApplication.delayCall += SafeAssetOperation;
        return;
    }
    
    // Check if we can modify files
    if (!EditorStateHelper.CanModifyProjectFiles)
    {
        Debug.LogWarning("Cannot modify assets in current state");
        return;
    }
    
    // Perform the operation
    AssetDatabase.Refresh();
}
```

### Using AssetOperationScope

```csharp
// Wrap complex asset operations to ensure proper state tracking
using (var scope = new AssetOperationScope())
{
    // Multiple asset operations
    for (int i = 0; i < 10; i++)
    {
        var asset = ScriptableObject.CreateInstance<MyAsset>();
        AssetDatabase.CreateAsset(asset, $"Assets/Asset_{i}.asset");
    }
    AssetDatabase.SaveAssets();
}
// Context automatically restored when scope is disposed
```

### Using Extension Methods

```csharp
// Use extension methods for automatic state tracking
AssetDatabaseExtensions.ImportAssetWithTracking("Assets/MyAsset.mat", ImportAssetOptions.ForceUpdate);
AssetDatabaseExtensions.RefreshWithTracking();
AssetDatabaseExtensions.SaveAssetsWithTracking();
```

## Components

### EditorStateHelper.cs
The main static class that tracks editor state. Features:
- Automatic initialization via [InitializeOnLoad]
- Persistent state storage across domain reloads via external StateStorage class
- Comprehensive event subscription to Unity Editor APIs
- Public properties and events for state access
- Notification methods for asset import tracking

### StateStorage.cs
Persistent storage for editor state:
- Survives domain reloads as a hidden ScriptableObject
- Tracks runmode, context, and transition states
- Manages asset import timing with timeout protection

### EditorStateAssetPostprocessor.cs
Asset postprocessor that ensures asset import operations are detected:
- Monitors texture, model, audio, animation, material, and prefab imports
- Provides manual notification methods for custom asset operations
- Includes AssetOperationScope for wrapping complex operations
- Extension methods for common AssetDatabase operations with tracking

### EditorStateMonitor.cs
An editor window for visualizing current editor state:
- Real-time state display with color coding
- State change log with timestamps
- Test actions for triggering state changes
- Access via menu: UMCP > Editor State Monitor

### EditorStateHelperExample.cs
Example code demonstrating:
- State-aware operations
- Event handling
- Menu item validation based on state
- Deferred operations during unsafe contexts

## Architecture Notes

- **Domain Reload Handling**: Uses external StateStorage ScriptableObject to persist state across domain reloads
- **Asset Import Detection**: Combines AssetPostprocessor callbacks with EditorApplication flags for comprehensive tracking
- **Timeout Protection**: Asset import state includes timeout to prevent getting stuck if import notifications are missed
- **Event-Driven**: Subscribes to multiple Unity Editor events for comprehensive state tracking
- **Thread-Safe**: All state changes are handled on the main thread via Unity's event system
- **Performance**: Minimal overhead with efficient state checking and event handling

## Best Practices

1. **Always Check State**: Before performing file operations, always check `CanModifyProjectFiles`
2. **Handle Transitions**: Use `EditorApplication.delayCall` to defer operations during state transitions
3. **Subscribe Responsibly**: Always unsubscribe from events in OnDisable/destructor
4. **Respect Context**: Don't force operations during Compiling or UpdatingAssets contexts
5. **Use Events**: Subscribe to state change events for reactive behavior rather than polling

## Troubleshooting

- If state seems incorrect, use `EditorStateHelper.RefreshState()` to force a state update
- Check the Editor State Monitor window for real-time state information
- Enable debug logging in the example class to see state transitions
- Remember that some operations may trigger multiple state changes in sequence

## Future Enhancements

Potential improvements could include:
- State history tracking
- Custom state definitions
- Performance metrics during different states
- Integration with Unity's Progress API
- Automated operation queuing for unsafe contexts