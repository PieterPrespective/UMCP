using System.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UMCPServer.Models;
using UMCPServer.Services;
using UMCPServer.Tools;

namespace UMCPServer.Tests.IntegrationTests.Tools;

/// <summary>
/// Integration tests for GetTests and RunTests tools following the specified test scenario
/// </summary>
[TestFixture]
[Category("Integration")]
public class ManageTestToolTests : IntegrationTestBase
{
    private Mock<ILogger<GetTestsTool>> _mockGetTestsLogger = null!;
    private Mock<ILogger<RunTestsTool>> _mockRunTestsLogger = null!;
    private Mock<ILogger<ForceUpdateEditorTool>> _mockForceUpdateLogger = null!;
    private Mock<ILogger<UnityConnectionService>> _mockUnityConnectionLogger = null!;
    private Mock<ILogger<UnityStateConnectionService>> _mockStateConnectionLogger = null!;
    private Mock<IUnityConnectionService> _mockUnityConnection = null!;
    private Mock<IUnityStateConnectionService> _mockStateConnection = null!;
    private GetTestsTool _getTestsTool = null!;
    private RunTestsTool _runTestsTool = null!;
    private ForceUpdateEditorTool _forceUpdateTool = null!;
    
    // Test data for SimpleEditModeTests
    private const string TestNamespace = "UMCP.editor.integrationtests";
    private const string TestScriptName = "SimpleEditModeTests";
    private const string TestAddition = $"{TestNamespace}.{TestScriptName}.TestAddition";
    private const string TestSubtraction = $"{TestNamespace}.{TestScriptName}.TestSubstraction";
    private const string TestFaulty = $"{TestNamespace}.{TestScriptName}.TestFaulty";
    
    [SetUp]
    public override void Setup()
    {
        base.Setup();
        
        // Setup loggers
        _mockGetTestsLogger = new Mock<ILogger<GetTestsTool>>();
        _mockRunTestsLogger = new Mock<ILogger<RunTestsTool>>();
        _mockForceUpdateLogger = new Mock<ILogger<ForceUpdateEditorTool>>();
        _mockUnityConnectionLogger = new Mock<ILogger<UnityConnectionService>>();
        _mockStateConnectionLogger = new Mock<ILogger<UnityStateConnectionService>>();
        
        // Setup config mocks
        var mockConfig = new Mock<IOptions<ServerConfiguration>>();
        mockConfig.Setup(x => x.Value).Returns(new ServerConfiguration());
        
        var mockStateConfig = new Mock<IOptions<ServerConfiguration>>();
        mockStateConfig.Setup(x => x.Value).Returns(new ServerConfiguration());
        
        // Setup Unity connection
        _mockUnityConnection = new Mock<IUnityConnectionService>();
        
        // Setup Unity state connection
        _mockStateConnection = new Mock<IUnityStateConnectionService>();
        
        // Create tool instances
        _getTestsTool = new GetTestsTool(_mockGetTestsLogger.Object, _mockUnityConnection.Object);
        _runTestsTool = new RunTestsTool(_mockRunTestsLogger.Object, _mockUnityConnection.Object);
        _forceUpdateTool = new ForceUpdateEditorTool(_mockForceUpdateLogger.Object, _mockUnityConnection.Object, _mockStateConnection.Object);
    }
    
    /// <summary>
    /// Main integration test that follows the specified scenario
    /// </summary>
    [Test]
    public void ManageTestTools_FullIntegrationTest()
    {
        ExecuteTestSteps(ManageTestToolsIntegrationSteps());
        Assert.That(TestCompleted, Is.True, "Test did not complete all steps");
    }
    
    private IEnumerator ManageTestToolsIntegrationSteps()
    {
        // Step 1: Setup mocks
        Console.WriteLine($"Step {CurrentStep + 1}: Setting up Unity connection mocks");
        _mockUnityConnection.Setup(m => m.IsConnected).Returns(true);
        _mockStateConnection.Setup(m => m.IsConnected).Returns(true);
        
        // Setup current Unity state for ForceUpdateEditor
        var currentState = new JObject
        {
            ["runmode"] = "EditMode_Scene",
            ["context"] = "Running",
            ["timestamp"] = DateTime.UtcNow.ToString("O")
        };
        _mockStateConnection.Setup(m => m.CurrentUnityState).Returns(currentState);
        yield return null;
        
        // Step 2: Validate Unity3D project is running with UMCP client active
        Console.WriteLine($"Step {CurrentStep + 1}: Validating Unity3D project with UMCP client");
        
        // Mock Unity state check
        var unityStateResponse = new JObject
        {
            ["status"] = "success",
            ["result"] = new JObject
            {
                ["runmode"] = "EditMode_Scene",
                ["context"] = "Running",
                ["canModifyProjectFiles"] = true
            }
        };
        
        _mockUnityConnection.Setup(m => m.SendCommandAsync(
            It.Is<string>(s => s == "get_unity_state"),
            It.IsAny<JObject>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(unityStateResponse);
        
        yield return null;
        
        // Step 2: Create simple EditMode unit tests
        Console.WriteLine($"Step {CurrentStep + 1}: Creating SimpleEditModeTests script");
        
        // In a real scenario, this would create the actual test files
        // For this integration test, we'll simulate it
        yield return null;
        
        // Step 3: Recompile using ForceUpdateEditor
        Console.WriteLine($"Step {CurrentStep + 1}: Forcing Unity Editor update for recompilation");
        
        var forceUpdateResponse = new JObject
        {
            ["success"] = true,
            ["data"] = new JObject
            {
                ["action"] = "force_update"
            }
        };
        
        _mockUnityConnection.Setup(m => m.SendCommandAsync(
            It.Is<string>(s => s == "force_update_editor"),
            It.IsAny<JObject>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(forceUpdateResponse);
        
        Task<object> forceUpdateTask = _forceUpdateTool.ForceUpdateEditor();
        yield return forceUpdateTask;
        
        var forceUpdateResult = forceUpdateTask.Result as dynamic;
        Assert.That(forceUpdateResult.success, Is.True, "Force update should succeed");
        yield return null;
        
        // Step 4: Test GetTests with all variations
        yield return TestGetTestsVariations();
        
        // Step 5: Run TestAddition only
        yield return RunTestAddition();
        
        // Step 6: Run TestFaulty and assert it fails
        yield return RunTestFaulty();
        
        // Step 7: Run all tests with no filter
        yield return RunAllTests();
        
        Console.WriteLine("Integration test completed successfully");
    }
    
    /// <summary>
    /// Tests all variations of GetTests tool parameters
    /// </summary>
    private IEnumerator TestGetTestsVariations()
    {
        Console.WriteLine($"Step {CurrentStep + 1}: Testing GetTests tool variations");
        
        // Test case 1: Get all tests (TestMode = "All")
        Console.WriteLine("Testing GetTests with TestMode='All'");
        
        var allTestsResponse = CreateGetTestsResponse(new[]
        {
            (TestAddition, TestNamespace, TestScriptName),
            (TestSubtraction, TestNamespace, TestScriptName),
            (TestFaulty, TestNamespace, TestScriptName),
            ("OtherNamespace.OtherTests.TestMethod", "OtherNamespace", "OtherTests")
        });
        
        _mockUnityConnection.Setup(m => m.SendCommandAsync(
            It.Is<string>(s => s == "get_tests"),
            It.Is<JObject>(j => j["TestMode"].ToString() == "All"),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(allTestsResponse);
        
        Task<object> getAllTask = _getTestsTool.GetTests("All");
        yield return getAllTask;
        
        var allResult = getAllTask.Result as dynamic;
        Assert.That(allResult.success, Is.True);
        Assert.That(allResult.count, Is.EqualTo(4));
        yield return null;
        
        // Test case 2: Get EditMode tests only
        Console.WriteLine("Testing GetTests with TestMode='EditMode'");
        
        var editModeResponse = CreateGetTestsResponse(new[]
        {
            (TestAddition, TestNamespace, TestScriptName),
            (TestSubtraction, TestNamespace, TestScriptName),
            (TestFaulty, TestNamespace, TestScriptName)
        });
        
        _mockUnityConnection.Setup(m => m.SendCommandAsync(
            It.Is<string>(s => s == "get_tests"),
            It.Is<JObject>(j => j["TestMode"].ToString() == "EditMode"),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(editModeResponse);
        
        Task<object> getEditModeTask = _getTestsTool.GetTests("EditMode");
        yield return getEditModeTask;
        
        var editModeResult = getEditModeTask.Result as dynamic;
        Assert.That(editModeResult.success, Is.True);
        Assert.That(editModeResult.count, Is.EqualTo(3));
        yield return null;
        
        // Test case 3: Get PlayMode tests only
        Console.WriteLine("Testing GetTests with TestMode='PlayMode'");
        
        var playModeResponse = CreateGetTestsResponse(new[]
        {
            ("OtherNamespace.OtherTests.TestMethod", "OtherNamespace", "OtherTests")
        });
        
        _mockUnityConnection.Setup(m => m.SendCommandAsync(
            It.Is<string>(s => s == "get_tests"),
            It.Is<JObject>(j => j["TestMode"].ToString() == "PlayMode"),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(playModeResponse);
        
        Task<object> getPlayModeTask = _getTestsTool.GetTests("PlayMode");
        yield return getPlayModeTask;
        
        var playModeResult = getPlayModeTask.Result as dynamic;
        Assert.That(playModeResult.success, Is.True);
        Assert.That(playModeResult.count, Is.EqualTo(1));
        yield return null;
        
        // Test case 4: Filter tests
        Console.WriteLine("Testing GetTests with Filter='SimpleEditMode'");
        
        var filteredResponse = CreateGetTestsResponse(new[]
        {
            (TestAddition, TestNamespace, TestScriptName),
            (TestSubtraction, TestNamespace, TestScriptName),
            (TestFaulty, TestNamespace, TestScriptName)
        });
        
        _mockUnityConnection.Setup(m => m.SendCommandAsync(
            It.Is<string>(s => s == "get_tests"),
            It.Is<JObject>(j => j["Filter"].ToString() == "SimpleEditMode"),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(filteredResponse);
        
        Task<object> getFilteredTask = _getTestsTool.GetTests("All", "SimpleEditMode");
        yield return getFilteredTask;
        
        var filteredResult = getFilteredTask.Result as dynamic;
        Assert.That(filteredResult.success, Is.True);
        Assert.That(filteredResult.count, Is.EqualTo(3));
        yield return null;
    }
    
    /// <summary>
    /// Runs TestAddition and validates it passes
    /// </summary>
    private IEnumerator RunTestAddition()
    {
        Console.WriteLine($"Step {CurrentStep + 1}: Running TestAddition");
        
        // Test with OutputTestResults = true, OutputLogData = true
        // Setup initial response
        _mockUnityConnection.Setup(m => m.SendCommandAsync(
            It.Is<string>(s => s == "run_tests"),
            It.Is<JObject>(j => 
                j["TestMode"].ToString() == "EditMode" &&
                j["Filter"] is JArray && 
                ((JArray)j["Filter"]).Count == 1 &&
                j["Filter"][0].ToString() == TestAddition),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(CreateRunTestsInitialResponse());
        
        // Setup console response sequence
        var sequence = _mockUnityConnection.SetupSequence(m => m.SendCommandAsync(
            It.Is<string>(s => s == "read_console"),
            It.IsAny<JObject>(),
            It.IsAny<CancellationToken>()
        ));
        
        // First poll - test still running
        sequence.ReturnsAsync(CreateConsoleResponse(false));
        
        // Second poll - test completed
        sequence.ReturnsAsync(CreateConsoleResponse(true, new[] { (TestAddition, true) }, true));
        
        Task<object> runAdditionTask = _runTestsTool.RunTests("EditMode", new[] { TestAddition }, true, true);
        yield return runAdditionTask;
        
        var additionResult = runAdditionTask.Result as JObject;
        Assert.That(additionResult["success"].Value<bool>(), Is.True);
        Assert.That(additionResult["AllSuccess"].Value<bool>(), Is.True);
        Assert.That(additionResult["TestResults"], Is.Not.Null);
        Assert.That(additionResult["LogData"], Is.Not.Null);
        yield return null;
        
        // Test with OutputTestResults = false, OutputLogData = false
        _mockUnityConnection.Setup(m => m.SendCommandAsync(
            It.Is<string>(s => s == "run_tests"),
            It.Is<JObject>(j => 
                j["OutputTestResults"].Value<bool>() == false &&
                j["OutputLogData"].Value<bool>() == false),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(CreateRunTestsInitialResponse());
        
        // Setup console response for no output test
        _mockUnityConnection.Setup(m => m.SendCommandAsync(
            It.Is<string>(s => s == "read_console"),
            It.IsAny<JObject>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(CreateConsoleResponse(true, new[] { (TestAddition, true) }, true));
        
        Task<object> runAdditionNoOutputTask = _runTestsTool.RunTests("EditMode", new[] { TestAddition }, false, false);
        yield return runAdditionNoOutputTask;
        
        var noOutputResult = runAdditionNoOutputTask.Result as JObject;
        Assert.That(noOutputResult["success"].Value<bool>(), Is.True);
        Assert.That(noOutputResult["AllSuccess"].Value<bool>(), Is.True);
        Assert.That(noOutputResult["TestResults"], Is.Null);
        Assert.That(noOutputResult["LogData"], Is.Null);
        yield return null;
    }
    
    /// <summary>
    /// Runs TestFaulty and validates it fails
    /// </summary>
    private IEnumerator RunTestFaulty()
    {
        Console.WriteLine($"Step {CurrentStep + 1}: Running TestFaulty (expecting failure)");
        
        // Setup initial response
        _mockUnityConnection.Setup(m => m.SendCommandAsync(
            It.Is<string>(s => s == "run_tests"),
            It.Is<JObject>(j => 
                j["Filter"] is JArray && 
                ((JArray)j["Filter"]).Count == 1 &&
                j["Filter"][0].ToString() == TestFaulty),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(CreateRunTestsInitialResponse());
        
        // Setup console response for failed test
        _mockUnityConnection.Setup(m => m.SendCommandAsync(
            It.Is<string>(s => s == "read_console"),
            It.IsAny<JObject>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(CreateConsoleResponse(true, new[] { (TestFaulty, false) }, false));
        
        Task<object> runFaultyTask = _runTestsTool.RunTests("EditMode", new[] { TestFaulty }, true, true);
        yield return runFaultyTask;
        
        var faultyResult = runFaultyTask.Result as JObject;
        Assert.That(faultyResult["success"].Value<bool>(), Is.True); // Tool succeeded
        Assert.That(faultyResult["AllSuccess"].Value<bool>(), Is.False); // But test failed
        
        var testResults = faultyResult["TestResults"] as JArray;
        Assert.That(testResults, Is.Not.Null);
        Assert.That(testResults.Count, Is.EqualTo(1));
        Assert.That(testResults[0]["Success"].Value<bool>(), Is.False);
        yield return null;
    }
    
    /// <summary>
    /// Runs all tests with no filter and validates TestFaulty still fails
    /// </summary>
    private IEnumerator RunAllTests()
    {
        Console.WriteLine($"Step {CurrentStep + 1}: Running all tests with no filter");
        
        // Setup initial response
        _mockUnityConnection.Setup(m => m.SendCommandAsync(
            It.Is<string>(s => s == "run_tests"),
            It.Is<JObject>(j => 
                j["TestMode"].ToString() == "EditMode" &&
                j["Filter"] is JArray && 
                ((JArray)j["Filter"]).Count == 0),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(CreateRunTestsInitialResponse());
        
        // Setup console response for all tests
        _mockUnityConnection.Setup(m => m.SendCommandAsync(
            It.Is<string>(s => s == "read_console"),
            It.IsAny<JObject>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(CreateConsoleResponse(true, new[]
        {
            (TestAddition, true),
            (TestSubtraction, true),
            (TestFaulty, false)
        }, false)); // allSuccess is false due to TestFaulty
        
        Task<object> runAllTask = _runTestsTool.RunTests("EditMode", null, true, true);
        yield return runAllTask;
        
        var allTestsResult = runAllTask.Result as JObject;
        Assert.That(allTestsResult["success"].Value<bool>(), Is.True);
        Assert.That(allTestsResult["AllSuccess"].Value<bool>(), Is.False); // Should be false due to TestFaulty
        
        var testResults = allTestsResult["TestResults"] as JArray;
        Assert.That(testResults, Is.Not.Null);
        Assert.That(testResults.Count, Is.EqualTo(3));
        
        // Verify individual test results
        var additionTest = testResults.FirstOrDefault(t => t["TestName"].ToString() == TestAddition);
        Assert.That(additionTest, Is.Not.Null);
        Assert.That(additionTest["Success"].Value<bool>(), Is.True);
        
        var subtractionTest = testResults.FirstOrDefault(t => t["TestName"].ToString() == TestSubtraction);
        Assert.That(subtractionTest, Is.Not.Null);
        Assert.That(subtractionTest["Success"].Value<bool>(), Is.True);
        
        var faultyTest = testResults.FirstOrDefault(t => t["TestName"].ToString() == TestFaulty);
        Assert.That(faultyTest, Is.Not.Null);
        Assert.That(faultyTest["Success"].Value<bool>(), Is.False);
        
        yield return null;
    }
    
    /// <summary>
    /// Helper method to create GetTests response
    /// </summary>
    private JObject CreateGetTestsResponse(params (string name, string ns, string script)[] tests)
    {
        var testArray = new JArray();
        foreach (var test in tests)
        {
            testArray.Add(new JObject
            {
                ["TestName"] = test.name,
                ["TestAssembly"] = "Assembly-CSharp-Editor",
                ["TestNamespace"] = test.ns,
                ["ContainerScript"] = test.script
            });
        }
        
        return new JObject
        {
            ["status"] = "success",
            ["result"] = new JObject(),
            ["message"] = $"Retrieved {tests.Length} tests.",
            ["data"] = testArray
        };
    }
    
    /// <summary>
    /// Helper method to create initial RunTests response (Unity returns immediately)
    /// </summary>
    private JObject CreateRunTestsInitialResponse()
    {
        return new JObject
        {
            ["success"] = true,
            ["message"] = "Test execution started successfully. Check Unity console for results or use RequestStepLogs to retrieve detailed results.",
            ["data"] = new JObject
            {
                ["message"] = "Tests are running in background. Results will appear in Unity console.",
                ["status"] = "running"
            }
        };
    }
    
    /// <summary>
    /// Helper method to create console log entries for test results
    /// </summary>
    private JObject CreateConsoleResponse(bool completed, (string name, bool success)[]? testResults = null, bool allSuccess = true)
    {
        var entries = new JArray();
        
        if (testResults != null)
        {
            foreach (var test in testResults)
            {
                var message = test.success 
                    ? $"[RunTests] ✓ PASS: {test.name} (0.123s)"
                    : $"[RunTests] ✗ FAIL: {test.name} (0.456s)";
                    
                entries.Add(new JObject
                {
                    ["type"] = test.success ? "Log" : "Error",
                    ["message"] = message
                });
            }
        }
        
        if (completed)
        {
            entries.Add(new JObject
            {
                ["type"] = "Log",
                ["message"] = $"[RunTests] Test execution completed. AllSuccess: {allSuccess}, Tests: {testResults?.Length ?? 0}"
            });
        }
        
        return new JObject
        {
            ["success"] = true,
            ["data"] = new JObject
            {
                ["entries"] = entries
            }
        };
    }
}