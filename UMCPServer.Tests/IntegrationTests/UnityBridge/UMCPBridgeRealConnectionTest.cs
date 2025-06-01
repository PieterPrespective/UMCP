using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Collections;
using UMCPServer.Models;
using UMCPServer.Services;
using UMCPServer.Tools;

namespace UMCPServer.Tests.IntegrationTests.UnityBridge;

[TestFixture]
[Category("Integration")]
public class UMCPBridgeRealConnectionTest : IntegrationTestBase
{
    private ServiceProvider? _serviceProvider;
    private UnityConnectionService? _unityConnection;
    private GetProjectPathTool? _getProjectPathTool;
    private string? _projectPath;
    
    private const int UnityPort = 6400;
    
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
        services.AddSingleton<GetProjectPathTool>();
        
        _serviceProvider = services.BuildServiceProvider();
        _unityConnection = _serviceProvider.GetRequiredService<UnityConnectionService>();
        _getProjectPathTool = _serviceProvider.GetRequiredService<GetProjectPathTool>();
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
    public void ConnectToRunningUnityAndGetProjectPath_ShouldReturnPath()
    {
        // Execute the multi-step integration test
        ExecuteTestSteps(ConnectAndGetProjectPathSteps());
        
        // Verify test completed all steps
        Assert.That(TestCompleted, Is.True, "Test did not complete all steps");
        
        // Verify we got a valid project path
        Assert.That(_projectPath, Is.Not.Null.And.Not.Empty, "Project path should not be null or empty");
    }


    private IEnumerator ConnectAndGetProjectPathSteps()
    {
        // Step 1: Check if Unity is running with UMCP
        Console.WriteLine($">>>> Step {CurrentStep + 1}: Checking if Unity is running with UMCP Client...");
        bool isUnityAvailable = false;
        bool portConnected = false;

        IsUnityPortOpen((_portOpen) => {
            Console.WriteLine($">>>> Step {CurrentStep + 1}a: Port open status: {_portOpen}");
            isUnityAvailable = _portOpen;
            portConnected = true;
        });

        yield return new WaitUntil(() => {
            return portConnected;
            });

        Console.WriteLine($"Got here!");

        if (!isUnityAvailable)
        {
            Console.WriteLine("Unity is not running with UMCP Client.");
            Console.WriteLine("Please:");
            Console.WriteLine("1. Open Unity Editor");
            Console.WriteLine($"2. Open the UMCPClient project at: C:\\Prespective\\250328_TestMLStuffUnity3d\\UMCP\\UMCPClient");
            Console.WriteLine("3. Ensure UMCP Bridge is running (it should start automatically)");
            Console.WriteLine("4. Run this test again");
            
            Assert.Ignore("Unity with UMCP Client is not running. This test requires Unity to be running.");
        }
        
        yield return null;
        
        // Step 2: Connect to Unity via UMCP Bridge
        Console.WriteLine($"Step {CurrentStep + 1}: Connecting to Unity via UMCP Bridge...");
        Task<bool> connectTask = _unityConnection!.ConnectAsync();
        yield return connectTask;
        
        Assert.That(connectTask.Result, Is.True, "Failed to connect to Unity");
        Console.WriteLine("Successfully connected to Unity!");
        
        // Step 3: Get project path from Unity
        Console.WriteLine($"Step {CurrentStep + 1}: Getting project path from Unity...");
        Task<object> getPathTask = _getProjectPathTool!.GetProjectPath();
        yield return getPathTask;
        
        // Step 4: Verify the project path
        Console.WriteLine($"Step {CurrentStep + 1}: Verifying project path result...");
        dynamic result = getPathTask.Result;
        Assert.That(result, Is.Not.Null, "Result should not be null");
        Assert.That(result.success, Is.True, "Project path request should be successful");
        Assert.That(result.projectPath, Is.Not.Null.And.Not.Empty, "Project path should not be empty");
        Console.WriteLine($"Retrieved project path: {result.projectPath}");
        

        // Additional verifications
        Assert.That(result.dataPath, Is.Not.Null.And.Not.Empty, "Data path should not be empty");
        Assert.That(result.persistentDataPath, Is.Not.Null.And.Not.Empty, "Persistent data path should not be empty");
        
        Console.WriteLine($"Data path: {result.dataPath}");
        Console.WriteLine($"Persistent data path: {result.persistentDataPath}");
        Console.WriteLine($"Streaming assets path: {result.streamingAssetsPath}");
        Console.WriteLine($"Temporary cache path: {result.temporaryCachePath}");
        
        // Step 5: Test ping command to ensure bridge is working properly
        Console.WriteLine($"Step {CurrentStep + 1}: Testing ping command...");
        bool pingIsDone = false;
        SendPingCommand(_unityConnection, (_pingResult) =>
        {
            Assert.That(_pingResult, Is.Not.Null, "Ping result should not be null");
            Assert.That(_pingResult?["message"]?.ToString(), Is.EqualTo("pong"), "Ping should return pong");
            pingIsDone = true;
        });

        yield return new WaitUntil(() => pingIsDone);

        
        // Step 6: Test other commands to demonstrate bridge functionality
        Console.WriteLine($"Step {CurrentStep + 1}: Testing editor state command...");

        bool editorStateIsDone = false;
        SendGetStateCommand(_unityConnection, (_editorStateResult) =>
        {
            Assert.That(_editorStateResult, Is.Not.Null, "Editor state result should not be null");
            editorStateIsDone = true;
        });
        yield return new WaitUntil(() => editorStateIsDone);
        _projectPath = result.projectPath;
        Console.WriteLine("Integration test completed successfully!");
        Console.WriteLine($"Final project path: {_projectPath}");
    }

    async private void SendGetStateCommand(UnityConnectionService _unityConnection, Action<JObject?> _onDone)
    {
        var stateResult = await _unityConnection.SendCommandAsync("manage_editor", 
            new JObject { ["action"] = "get_state" });
        _onDone(stateResult);
    }



    async private void SendPingCommand(UnityConnectionService _unityConnection, Action<JObject?> _onDone)
    {
        var pingResult = await _unityConnection.SendCommandAsync("ping", null);
        _onDone(pingResult);
    }




    private async void IsUnityPortOpen(Action<bool> _onPortOpen)
    {
        try
        {
            using (var client = new System.Net.Sockets.TcpClient())
            {
                // Use shorter connection timeout for initial attempts
                var connectTask = client.ConnectAsync("localhost", UnityPort);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10d));
                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    throw new TimeoutException($"Connection to localhost:{UnityPort} timed out");
                }
                _onPortOpen(true);
            }
        }
        catch(Exception _ex)
        {
            Console.WriteLine($">>>> Step {CurrentStep + 1}a: Failed to connect at localhost:{UnityPort}. Exception: {_ex.Message}");
            // If we can't connect, we assume the port is not open
            Console.WriteLine("Assuming Unity is not running with UMCP Client.");
            
            _onPortOpen(false);
        }
    }
}
