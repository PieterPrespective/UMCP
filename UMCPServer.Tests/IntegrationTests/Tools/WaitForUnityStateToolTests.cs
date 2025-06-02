using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UMCPServer.Services;
using UMCPServer.Tools;

namespace UMCPServer.Tests.IntegrationTests.Tools
{
    [TestFixture]
    public class WaitForUnityStateToolTests : IntegrationTestBase
    {
        private WaitForUnityStateTool _tool;
        private Mock<ILogger<WaitForUnityStateTool>> _mockLogger;
        private Mock<UnityConnectionService> _mockUnityConnection;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<WaitForUnityStateTool>>();
            _mockUnityConnection = new Mock<UnityConnectionService>(
                Mock.Of<ILogger<UnityConnectionService>>(),
                Microsoft.Extensions.Options.Options.Create(new Models.ServerConfiguration())
            );
            
            _tool = new WaitForUnityStateTool(_mockLogger.Object, _mockUnityConnection.Object);
        }

        [Test]
        public async Task WaitForUnityState_WithNoTargetSpecified_ReturnsError()
        {
            // Act
            var result = await _tool.WaitForUnityState(null, null, 1000);

            // Assert
            dynamic dynamicResult = result;
            Assert.That(dynamicResult.success, Is.False);
            Assert.That(dynamicResult.error, Does.Contain("At least one of targetRunmode or targetContext must be specified"));
        }

        [Test]
        public async Task WaitForUnityState_WhenUnityNotConnected_ReturnsError()
        {
            // Arrange
            _mockUnityConnection.Setup(x => x.IsConnected).Returns(false);
            _mockUnityConnection.Setup(x => x.ConnectAsync()).ReturnsAsync(false);

            // Act
            var result = await _tool.WaitForUnityState("EditMode_Scene", null, 1000);

            // Assert
            dynamic dynamicResult = result;
            Assert.That(dynamicResult.success, Is.False);
            Assert.That(dynamicResult.error, Does.Contain("Unity Editor is not running"));
        }

        [Test]
        public async Task WaitForUnityState_WhenAlreadyInDesiredState_ReturnsImmediately()
        {
            // Arrange
            var currentState = new JObject
            {
                ["runmode"] = "EditMode_Scene",
                ["context"] = "Running",
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };

            _mockUnityConnection.Setup(x => x.IsConnected).Returns(true);
            _mockUnityConnection.Setup(x => x.CurrentUnityState).Returns(currentState);

            // Act
            var result = await _tool.WaitForUnityState("EditMode_Scene", "Running", 5000);

            // Assert
            dynamic dynamicResult = result;
            Assert.That(dynamicResult.success, Is.True);
            Assert.That(dynamicResult.message, Does.Contain("already in the desired state"));
            Assert.That(dynamicResult.waitTimeMs, Is.EqualTo(0));
        }

        [Test]
        public async Task WaitForUnityState_WhenStateChanges_ReturnsOnStateChange()
        {
            // Arrange
            var initialState = new JObject
            {
                ["runmode"] = "EditMode_Scene",
                ["context"] = "Running",
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };

            var targetState = new JObject
            {
                ["runmode"] = "PlayMode",
                ["context"] = "Running",
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };

            _mockUnityConnection.Setup(x => x.IsConnected).Returns(true);
            _mockUnityConnection.Setup(x => x.CurrentUnityState).Returns(initialState);

            // Setup event
            Action<JObject> stateChangedHandler = null;
            _mockUnityConnection.SetupAdd(x => x.UnityStateChanged += It.IsAny<Action<JObject>>())
                .Callback<Action<JObject>>(handler => stateChangedHandler = handler);

            // Act
            var waitTask = _tool.WaitForUnityState("PlayMode", "Running", 5000);

            // Simulate state change after a short delay
            await Task.Delay(100);
            stateChangedHandler?.Invoke(targetState);

            var result = await waitTask;

            // Assert
            dynamic dynamicResult = result;
            Assert.That(dynamicResult.success, Is.True);
            Assert.That(dynamicResult.runmode, Is.EqualTo("PlayMode"));
            Assert.That(dynamicResult.context, Is.EqualTo("Running"));
            Assert.That(dynamicResult.waitTimeMs, Is.GreaterThan(0));
        }

        [Test]
        public async Task WaitForUnityState_WhenTimeout_ReturnsError()
        {
            // Arrange
            var currentState = new JObject
            {
                ["runmode"] = "EditMode_Scene",
                ["context"] = "Running",
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };

            _mockUnityConnection.Setup(x => x.IsConnected).Returns(true);
            _mockUnityConnection.Setup(x => x.CurrentUnityState).Returns(currentState);

            // Act
            var result = await _tool.WaitForUnityState("PlayMode", "Running", 500); // Short timeout

            // Assert
            dynamic dynamicResult = result;
            Assert.That(dynamicResult.success, Is.False);
            Assert.That(dynamicResult.error, Does.Contain("Timeout waiting for Unity state"));
            Assert.That(dynamicResult.currentRunmode, Is.EqualTo("EditMode_Scene"));
            Assert.That(dynamicResult.currentContext, Is.EqualTo("Running"));
            Assert.That(dynamicResult.targetRunmode, Is.EqualTo("PlayMode"));
        }

        [Test]
        public async Task WaitForUnityState_WithOnlyRunmodeSpecified_IgnoresContext()
        {
            // Arrange
            var currentState = new JObject
            {
                ["runmode"] = "EditMode_Scene",
                ["context"] = "Compiling",
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };

            var targetState = new JObject
            {
                ["runmode"] = "PlayMode",
                ["context"] = "Switching", // Different context
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };

            _mockUnityConnection.Setup(x => x.IsConnected).Returns(true);
            _mockUnityConnection.Setup(x => x.CurrentUnityState).Returns(currentState);

            Action<JObject> stateChangedHandler = null;
            _mockUnityConnection.SetupAdd(x => x.UnityStateChanged += It.IsAny<Action<JObject>>())
                .Callback<Action<JObject>>(handler => stateChangedHandler = handler);

            // Act
            var waitTask = _tool.WaitForUnityState("PlayMode", null, 5000);

            await Task.Delay(100);
            stateChangedHandler?.Invoke(targetState);

            var result = await waitTask;

            // Assert
            dynamic dynamicResult = result;
            Assert.That(dynamicResult.success, Is.True);
            Assert.That(dynamicResult.runmode, Is.EqualTo("PlayMode"));
        }

        [Test]
        public async Task WaitForUnityState_WithOnlyContextSpecified_IgnoresRunmode()
        {
            // Arrange
            var currentState = new JObject
            {
                ["runmode"] = "EditMode_Scene",
                ["context"] = "Compiling",
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };

            var targetState = new JObject
            {
                ["runmode"] = "EditMode_Prefab", // Different runmode
                ["context"] = "Running",
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };

            _mockUnityConnection.Setup(x => x.IsConnected).Returns(true);
            _mockUnityConnection.Setup(x => x.CurrentUnityState).Returns(currentState);

            Action<JObject> stateChangedHandler = null;
            _mockUnityConnection.SetupAdd(x => x.UnityStateChanged += It.IsAny<Action<JObject>>())
                .Callback<Action<JObject>>(handler => stateChangedHandler = handler);

            // Act
            var waitTask = _tool.WaitForUnityState(null, "Running", 5000);

            await Task.Delay(100);
            stateChangedHandler?.Invoke(targetState);

            var result = await waitTask;

            // Assert
            dynamic dynamicResult = result;
            Assert.That(dynamicResult.success, Is.True);
            Assert.That(dynamicResult.context, Is.EqualTo("Running"));
        }
    }
}
