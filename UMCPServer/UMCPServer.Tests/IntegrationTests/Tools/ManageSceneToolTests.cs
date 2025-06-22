using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Collections;
using UMCPServer.Models;
using UMCPServer.Services;
using UMCPServer.Tools;

namespace UMCPServer.Tests.IntegrationTests.Tools;

[TestFixture]
[Category("Integration")]
public class ManageSceneToolTests : IntegrationTestBase
{
    private ServiceProvider? _serviceProvider;
    private UnityConnectionService? _unityConnection;
    private ManageSceneTool? _manageSceneTool;
    private ExecuteMenuItemTool? _executeMenuItemTool;
    
    private const int UnityPort = 6400;
    private const string TestSceneName = "TestIntegrationScene";
    private const string TestScenePath = "Scenes/IntegrationTests";
    
    [SetUp]
    public override void Setup()
    {
        base.Setup();
        
        // Set up dependency injection
        var services = new ServiceCollection();
        
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        // Configure server settings
        services.Configure<ServerConfiguration>(options =>
        {
            options.UnityHost = "localhost";
            options.UnityPort = UnityPort;
            options.ConnectionTimeoutSeconds = 30;
            options.MaxRetries = 5;
            options.RetryDelaySeconds = 2;
            options.BufferSize = 8192;
            options.IsRunningInContainer = false;
        });
        
        // Register services
        services.AddSingleton<UnityConnectionService>();
        services.AddSingleton<ManageSceneTool>();
        services.AddSingleton<ExecuteMenuItemTool>();
        
        _serviceProvider = services.BuildServiceProvider();
        _unityConnection = _serviceProvider.GetRequiredService<UnityConnectionService>();
        _manageSceneTool = _serviceProvider.GetRequiredService<ManageSceneTool>();
        _executeMenuItemTool = _serviceProvider.GetRequiredService<ExecuteMenuItemTool>();
    }
    
    [TearDown]
    public override void TearDown()
    {
        // Cleanup services
        _unityConnection?.Dispose();
        _serviceProvider?.Dispose();
        
        base.TearDown();
    }
    
    [Test]
    [Category("RequiresUnity")]
    public void ManageScene_CreateSaveLoadAndModify_ShouldWorkCorrectly()
    {
        ExecuteTestSteps(ManageSceneIntegrationSteps());
        Assert.That(TestCompleted, Is.True, "Test did not complete all steps");
    }
    
    private IEnumerator ManageSceneIntegrationSteps()
    {
        // Step 1: Check if Unity is running
        Console.WriteLine($"Step {CurrentStep + 1}: Checking if Unity is running with UMCP Client...");
        bool isUnityAvailable = false;
        bool portConnected = false;

        IsUnityPortOpen((portOpen) => {
            isUnityAvailable = portOpen;
            portConnected = true;
        });

        yield return new WaitUntil(() => portConnected);

        if (!isUnityAvailable)
        {
            Console.WriteLine("Unity is not running with UMCP Client. Skipping this test.");
            Assert.Ignore("Unity with UMCP Client is not running. This test requires Unity to be running.");
        }
        
        yield return null;
        
        // Step 2: Connect to Unity
        Console.WriteLine($"Step {CurrentStep + 1}: Connecting to Unity...");
        Task<bool> connectTask = _unityConnection!.ConnectAsync();
        yield return connectTask;
        
        Assert.That(connectTask.Result, Is.True, "Failed to connect to Unity");
        Console.WriteLine("Successfully connected to Unity!");
        yield return null;
        
        // Step 3: Create a new scene with a specific name
        Console.WriteLine($"Step {CurrentStep + 1}: Creating new scene '{TestSceneName}'...");
        Task<object> createSceneTask = _manageSceneTool!.ManageScene(
            action: "create",
            name: TestSceneName,
            path: TestScenePath
        );
        yield return createSceneTask;
        
        dynamic createResult = createSceneTask.Result;
        Assert.That(createResult, Is.Not.Null, "Create scene result should not be null");
        Assert.That((bool)createResult.success, Is.True, $"Failed to create scene: {createResult.error}");
        
        Console.WriteLine($"Scene created successfully at: {createResult.path}");
        yield return null;
        
        // Step 4: Save the scene (it should already be saved from create, but we'll ensure it)
        Console.WriteLine($"Step {CurrentStep + 1}: Saving the scene...");
        Task<object> saveSceneTask = _manageSceneTool.ManageScene(
            action: "save"
        );
        yield return saveSceneTask;
        
        dynamic saveResult = saveSceneTask.Result;
        Assert.That(saveResult, Is.Not.Null, "Save scene result should not be null");
        Assert.That((bool)saveResult.success, Is.True, $"Failed to save scene: {saveResult.error}");
        
        Console.WriteLine($"Scene saved successfully at: {saveResult.path}");
        yield return null;
        
        // Step 5: Load the scene
        Console.WriteLine($"Step {CurrentStep + 1}: Loading the scene...");
        Task<object> loadSceneTask = _manageSceneTool.ManageScene(
            action: "load",
            name: TestSceneName,
            path: $"{TestScenePath}/{TestSceneName}.unity"
        );
        yield return loadSceneTask;
        
        dynamic loadResult = loadSceneTask.Result;
        Assert.That(loadResult, Is.Not.Null, "Load scene result should not be null");
        Assert.That((bool)loadResult.success, Is.True, $"Failed to load scene: {loadResult.error}");
        
        Console.WriteLine($"Scene loaded successfully: {loadResult.name}");
        yield return null;
        
        // Step 6: Execute menu item to create 3 cubes
        Console.WriteLine($"Step {CurrentStep + 1}: Creating 3 cubes using menu items...");
        
        for (int i = 0; i < 3; i++)
        {
            Console.WriteLine($"  Creating cube {i + 1}...");
            Task<object> createCubeTask = _executeMenuItemTool!.ExecuteMenuItem(
                action: "execute",
                menuPath: "GameObject/3D Object/Cube"
            );
            yield return createCubeTask;
            
            dynamic cubeResult = createCubeTask.Result;
            Assert.That(cubeResult, Is.Not.Null, $"Create cube {i + 1} result should not be null");
            Assert.That((bool)cubeResult.success, Is.True, $"Failed to create cube {i + 1}: {cubeResult.error}");
            
            // Small delay between cube creations to ensure they get unique names
            yield return new WaitForSeconds(0.5f);
        }
        
        Console.WriteLine("All 3 cubes created successfully!");
        yield return null;
        
        // Step 7: Get hierarchy and verify the GameObjects
        Console.WriteLine($"Step {CurrentStep + 1}: Getting scene hierarchy...");
        Task<object> getHierarchyTask = _manageSceneTool.ManageScene(
            action: "get_hierarchy"
        );
        yield return getHierarchyTask;
        
        dynamic hierarchyResult = getHierarchyTask.Result;
        Assert.That(hierarchyResult, Is.Not.Null, "Get hierarchy result should not be null");
        Assert.That((bool)hierarchyResult.success, Is.True, $"Failed to get hierarchy: {hierarchyResult.error}");
        
        // Verify the cubes exist with correct names
        var hierarchy = hierarchyResult.hierarchy as IEnumerable<dynamic>;
        Assert.That(hierarchy, Is.Not.Null, "Hierarchy should not be null");
        
        var gameObjects = hierarchy.ToList();
        Console.WriteLine($"Found {gameObjects.Count} root GameObjects in the scene");
        
        // Find the cubes
        var cubeNames = new List<string> { "Cube", "Cube (1)", "Cube (2)" };
        var foundCubes = new List<string>();
        
        foreach (dynamic gameObject in gameObjects)
        {
            string name = gameObject.name;
            Console.WriteLine($"  Found GameObject: {name}");
            
            if (cubeNames.Contains(name))
            {
                foundCubes.Add(name);
            }
        }
        
        // Verify all expected cubes were found
        Assert.That(foundCubes.Count, Is.EqualTo(3), $"Expected 3 cubes but found {foundCubes.Count}");
        foreach (string expectedCube in cubeNames)
        {
            Assert.That(foundCubes.Contains(expectedCube), Is.True, $"Expected to find '{expectedCube}' in the hierarchy");
        }
        
        Console.WriteLine("All expected cubes found in the hierarchy!");
        yield return null;
        
        // Step 8: Get active scene info
        Console.WriteLine($"Step {CurrentStep + 1}: Getting active scene information...");
        Task<object> getActiveTask = _manageSceneTool.ManageScene(
            action: "get_active"
        );
        yield return getActiveTask;
        
        dynamic activeResult = getActiveTask.Result;
        Assert.That(activeResult, Is.Not.Null, "Get active scene result should not be null");
        Assert.That((bool)activeResult.success, Is.True, $"Failed to get active scene: {activeResult.error}");
        
        Console.WriteLine($"Active scene: {activeResult.name}");
        Console.WriteLine($"  Path: {activeResult.path}");
        Console.WriteLine($"  Is Dirty: {activeResult.isDirty}");
        Console.WriteLine($"  Root Count: {activeResult.rootCount}");
        yield return null;
        
        // Step 9: Cleanup - Note: In a real scenario, you might want to delete the test scene
        Console.WriteLine($"Step {CurrentStep + 1}: Test completed successfully!");
        Console.WriteLine("Note: The test scene remains in the project. Consider manual cleanup if needed.");
        yield return null;
        
        Console.WriteLine("Integration test completed!");
    }
    
    private async void IsUnityPortOpen(Action<bool> onPortOpen)
    {
        try
        {
            using (var client = new System.Net.Sockets.TcpClient())
            {
                var connectTask = client.ConnectAsync("localhost", UnityPort);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                
                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    throw new TimeoutException($"Connection to localhost:{UnityPort} timed out");
                }
                
                onPortOpen(true);
            }
        }
        catch
        {
            onPortOpen(false);
        }
    }
}

//// Helper class for waiting
//public class WaitUntil : YieldInstruction
//{
//    private readonly Func<bool> _predicate;
//    private readonly System.Diagnostics.Stopwatch _stopwatch;

//    public WaitUntil(Func<bool> predicate)
//    {
//        _predicate = predicate;
//        _stopwatch = System.Diagnostics.Stopwatch.StartNew();
//        TimeoutDuration = 30000; // 30 seconds default timeout
//    }

//    public override bool IsDone => _predicate() || _stopwatch.ElapsedMilliseconds >= TimeoutDuration;
//}

// Helper class for waiting with time
public class WaitForSeconds : YieldInstruction
{
    private readonly System.Diagnostics.Stopwatch _stopwatch;
    private readonly float _seconds;

    public WaitForSeconds(float seconds)
    {
        _seconds = seconds;
        _stopwatch = System.Diagnostics.Stopwatch.StartNew();
    }

    public override bool IsDone => _stopwatch.ElapsedMilliseconds >= (_seconds * 1000);
}

// Base class for yield instructions
//public abstract class YieldInstruction
//{
//    public abstract bool IsDone { get; }
//    public int TimeoutDuration { get; set; } = 30000;
//}
