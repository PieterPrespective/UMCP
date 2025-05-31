using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UMCPServer.Models;
using UMCPServer.Services;
using UMCPServer.Tools;
using static System.Net.Mime.MediaTypeNames;

namespace UMCPServer.Tests.IntegrationTests.UnityBridge;

[TestFixture]
[Category("Integration")]
public class UMCPBridgeIntegrationTest : IntegrationTestBase
{
    private Process? _unityProcess;
    private ServiceProvider? _serviceProvider;
    private UnityConnectionService? _unityConnection;
    private GetProjectPathTool? _getProjectPathTool;
    private string? _projectPath;
    
    private const string UnityProjectPath = @"C:\Prespective\250328_TestMLStuffUnity3d\UMCP\UMCPClient";
    private const string UnityExecutablePath = @"C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe"; // Adjust version as needed
    private const int UnityPort = 6400;
    private const int MaxStartupWaitSeconds = 60;
    
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
        // Cleanup Unity process
        if (_unityProcess != null && !_unityProcess.HasExited)
        {
            Console.WriteLine("Shutting down Unity process...");
            _unityProcess.Kill();
            _unityProcess.WaitForExit(5000);
            _unityProcess.Dispose();
        }
        
        // Cleanup services
        _unityConnection?.Dispose();
        _serviceProvider?.Dispose();
        
        base.TearDown();
    }
    
    [Test]
    public void CreateUMCPBridgeAndGetProjectPath_ShouldConnectToUnityAndReturnPath()
    {
        // Execute the multi-step integration test
        ExecuteTestSteps(CreateBridgeAndGetProjectPathSteps());
        
        // Verify test completed all steps
        Assert.That(TestCompleted, Is.True, "Test did not complete all steps");
        
        // Verify we got a valid project path
        Assert.That(_projectPath, Is.Not.Null.And.Not.Empty, "Project path should not be null or empty");
        Assert.That(_projectPath, Does.Contain("UMCPClient"), "Project path should contain UMCPClient");
    }
    
    private IEnumerator CreateBridgeAndGetProjectPathSteps()
    {
        // Step 1: Start Unity in headless/batch mode
        Console.WriteLine($"Step {CurrentStep + 1}: Starting Unity in headless mode...");
        yield return StartUnityInHeadlessMode();
        
        // Step 2: Wait for Unity to fully initialize
        Console.WriteLine($"Step {CurrentStep + 1}: Waiting for Unity to initialize...");
        yield return WaitForUnityInitialization();
        
        // Step 3: Connect to Unity via UMCP Bridge
        Console.WriteLine($"Step {CurrentStep + 1}: Connecting to Unity via UMCP Bridge...");
        Task<bool> connectTask = _unityConnection!.ConnectAsync();
        yield return connectTask;
        
        Assert.That(connectTask.Result, Is.True, "Failed to connect to Unity");
        Console.WriteLine("Successfully connected to Unity!");
        
        // Step 4: Get project path from Unity
        Console.WriteLine($"Step {CurrentStep + 1}: Getting project path from Unity...");
        Task<object> getPathTask = _getProjectPathTool!.GetProjectPath();
        yield return getPathTask;
        
        // Step 5: Verify the project path
        Console.WriteLine($"Step {CurrentStep + 1}: Verifying project path result...");
        var result = getPathTask.Result;
        Assert.That(result, Is.Not.Null, "Result should not be null");
        
        // Extract project path from result
        dynamic resultObj = result;
        Assert.That(resultObj.success, Is.True, "Project path request should be successful");
        Assert.That(resultObj.projectPath, Is.Not.Null.And.Not.Empty, "Project path should not be empty");
        
        _projectPath = resultObj.projectPath.ToString();
        Console.WriteLine($"Retrieved project path: {_projectPath}");
        
        // Verify it's the expected project
        Assert.That(_projectPath, Does.Contain("UMCPClient"), "Project path should be for UMCPClient");
        
        // Additional verifications
        Assert.That(resultObj.dataPath, Is.Not.Null.And.Not.Empty, "Data path should not be empty");
        Assert.That(resultObj.persistentDataPath, Is.Not.Null.And.Not.Empty, "Persistent data path should not be empty");
        
        Console.WriteLine($"Data path: {resultObj.dataPath}");
        Console.WriteLine($"Persistent data path: {resultObj.persistentDataPath}");
        
        // Step 6: Test another command to ensure bridge is working properly
        Console.WriteLine($"Step {CurrentStep + 1}: Testing ping command...");

        bool pingIsDone = false;
        SendPingCommand(_unityConnection, (_pingResult) =>
        {
            Assert.That(_pingResult, Is.Not.Null, "Ping result should not be null");
            Assert.That(_pingResult?["message"]?.ToString(), Is.EqualTo("pong"), "Ping should return pong");
            pingIsDone = true;
        });

        yield return new WaitUntil(() => pingIsDone);
        //var pingResult = await _unityConnection.SendCommandAsync("ping", null);
       
        
        Console.WriteLine("Integration test completed successfully!");
    }


    




    async private void SendPingCommand(UnityConnectionService _unityConnection, Action<JObject?> _onDone)
    {
        var pingResult = await _unityConnection.SendCommandAsync("ping", null);
        _onDone(pingResult);
    }


    private IEnumerator StartUnityInHeadlessMode()
    {
        // Check if Unity executable exists
        if (!File.Exists(UnityExecutablePath))
        {
            // Try to find Unity installation
            string unityPath = FindUnityExecutable();
            if (string.IsNullOrEmpty(unityPath))
            {
                throw new FileNotFoundException(
                    $"Unity executable not found at {UnityExecutablePath}. " +
                    "Please update the path or ensure Unity is installed.");
            }
        }
        
        // Prepare Unity command line arguments for headless mode
        // Note: -batchmode runs Unity without graphics, -nographics ensures no GPU is used
        // -quit will make Unity exit after execution, but we don't want that for our bridge
        var startInfo = new ProcessStartInfo
        {
            FileName = File.Exists(UnityExecutablePath) ? UnityExecutablePath : FindUnityExecutable(),
            Arguments = $"-projectPath \"{UnityProjectPath}\" -batchmode -nographics -logFile -",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        _unityProcess = new Process { StartInfo = startInfo };
        
        // Capture Unity output for debugging
        _unityProcess.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine($"[Unity] {e.Data}");
        };
        
        _unityProcess.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine($"[Unity Error] {e.Data}");
        };
        
        // Start Unity
        bool started = _unityProcess.Start();
        Assert.That(started, Is.True, "Failed to start Unity process");
        
        _unityProcess.BeginOutputReadLine();
        _unityProcess.BeginErrorReadLine();
        
        Console.WriteLine($"Unity process started with PID: {_unityProcess.Id}");
        yield return null;
    }
    
    private IEnumerator WaitForUnityInitialization()
    {
        // Wait for Unity to fully initialize and UMCP Bridge to start
        var stopwatch = Stopwatch.StartNew();
        bool unityReady = false;
        
        while (!unityReady && stopwatch.Elapsed.TotalSeconds < MaxStartupWaitSeconds)
        {
            // Check if Unity process is still running
            if (_unityProcess!.HasExited)
            {
                throw new Exception($"Unity process exited unexpectedly with code: {_unityProcess.ExitCode}");
            }
            
            // Try to connect to see if UMCP Bridge is ready
            try
            {
                using var testClient = new System.Net.Sockets.TcpClient();
                var connectTask = testClient.ConnectAsync("localhost", UnityPort);
                if (connectTask.Wait(TimeSpan.FromSeconds(1)))
                {
                    unityReady = true;
                    testClient.Close();
                    Console.WriteLine("Unity UMCP Bridge is ready!");
                }
            }
            catch
            {
                // Not ready yet, continue waiting
            }
            
            if (!unityReady)
            {
                Console.WriteLine($"Waiting for Unity to initialize... ({stopwatch.Elapsed.TotalSeconds:F1}s)");
                yield return Task.Delay(2000);
            }
        }
        
        if (!unityReady)
        {
            throw new TimeoutException($"Unity failed to initialize within {MaxStartupWaitSeconds} seconds");
        }
        
        // Give Unity a bit more time to fully stabilize
        yield return Task.Delay(2000);
    }
    
    private string FindUnityExecutable()
    {
        // Common Unity installation paths
        string[] possiblePaths = new[]
        {
            @"C:\Program Files\Unity\Hub\Editor",
            @"C:\Program Files (x86)\Unity\Hub\Editor",
            @"C:\Unity\Hub\Editor"
        };
        
        foreach (var basePath in possiblePaths)
        {
            if (Directory.Exists(basePath))
            {
                // Look for Unity versions
                var versionDirs = Directory.GetDirectories(basePath);
                foreach (var versionDir in versionDirs.OrderByDescending(d => d))
                {
                    string unityExe = Path.Combine(versionDir, "Editor", "Unity.exe");
                    if (File.Exists(unityExe))
                    {
                        Console.WriteLine($"Found Unity at: {unityExe}");
                        return unityExe;
                    }
                }
            }
        }
        
        return string.Empty;
    }
    
    private async Task<bool> IsUnityPortOpen()
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync("localhost", UnityPort);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public abstract class YieldInstruction
{
    public abstract bool IsDone { get; }
}

// WaitUntil implementation
public class WaitUntil : YieldInstruction
{
    private readonly Func<bool> _predicate;

    public WaitUntil(Func<bool> predicate)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public override bool IsDone => _predicate();
}


