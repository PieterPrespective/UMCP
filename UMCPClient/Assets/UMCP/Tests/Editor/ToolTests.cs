using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Collections;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UMCP.Editor;
using UMCP.Editor.Models;
using UMCP.Editor.Tools;
using UnityEngine;
using UnityEngine.TestTools;

namespace UMCP.Tests.Editor
{
    /// <summary>
    /// Class with tests for the UMCP Tool functionalities.
    /// </summary>
    public class ToolTests 
    {
        private const int unityPort = 6400;  // Same port as UMCPBridge

        [UnityTest]
        public IEnumerator TestTool_GetProjectPath()
        {
            // Ensure bridge is running
            if (!UMCPBridge.IsRunning)
            {
                UMCPBridge.Start();
                // Wait a moment for the bridge to start
                yield return new WaitForSeconds(0.5f);
            }

            // Create a command to request the project path
            Command command = new Command
            {
                type = "get_project_path",
                @params = new JObject()
            };

            string commandJson = JsonConvert.SerializeObject(command);
            byte[] commandBytes = Encoding.UTF8.GetBytes(commandJson);

            // Connect to the UMCPBridge via TCP
            using (TcpClient client = new TcpClient())
            {
                // Connect to the bridge
                var connectTask = ConnectToUMCPBridge(client);
                while (!connectTask.IsCompleted)
                    yield return null;

                if (!connectTask.Result)
                {
                    Assert.Fail("Failed to connect to UMCPBridge");
                    yield break;
                }

                // Get the network stream
                using (NetworkStream stream = client.GetStream())
                {
                    // Send the command
                    stream.Write(commandBytes, 0, commandBytes.Length);

                    // Wait for response
                    byte[] buffer = new byte[8192];
                    var readTask = ReadFromStreamAsync(stream, buffer);
                    while (!readTask.IsCompleted)
                        yield return null;

                    int bytesRead = readTask.Result;
                    if (bytesRead == 0)
                    {
                        Assert.Fail("Received empty response");
                        yield break;
                    }

                    // Parse the response
                    string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    JObject response = JObject.Parse(responseJson);

                    // Verify response structure
                    Assert.IsNotNull(response["status"], "Response should contain a 'status' field");
                    Assert.AreEqual("success", response["status"].ToString(), "Expected status to be 'success'");
                    Assert.IsNotNull(response["result"], "Response should contain a 'result' field");

                    // Validate project path data
                    JObject result = response["result"] as JObject;
                    Assert.IsNotNull(result, "Result should be a JSON object");
                    
                    // Log the full result JObject for debugging
                    Debug.Log($"GetProjectPath result: {result.ToString(Formatting.Indented)}");
                    
                    // The path data is nested in the "data" property
                    Assert.IsNotNull(result["data"], "Result should contain a 'data' field");
                    JObject pathData = result["data"] as JObject;
                    Assert.IsNotNull(pathData, "The data field should be a JSON object");
                    
                    // Verify the data paths
                    Assert.IsNotNull(pathData["dataPath"], "Data should contain 'dataPath'");
                    Assert.IsNotNull(pathData["projectPath"], "Data should contain 'projectPath'");
                    Assert.IsNotNull(pathData["persistentDataPath"], "Data should contain 'persistentDataPath'");
                    Assert.IsNotNull(pathData["streamingAssetsPath"], "Data should contain 'streamingAssetsPath'");
                    Assert.IsNotNull(pathData["temporaryCachePath"], "Data should contain 'temporaryCachePath'");

                    // Verify that the dataPath is correct
                    Assert.AreEqual(Application.dataPath, pathData["dataPath"].ToString(), 
                        "dataPath in response should match Application.dataPath");
                    
                    // Verify that projectPath + "Assets" equals dataPath
                    string projectPath = pathData["projectPath"].ToString();

                    //NOTE : the following only works on Windows, but is not necessary on other platforms
                    Assert.AreEqual(Application.dataPath, projectPath +"/Assets", 
                        "projectPath + 'Assets' should equal Application.dataPath");
                }
            }
        }

        [UnityTest]
        public IEnumerator TestTool_CommandRegistry()
        {
            // Ensure bridge is running
            if (!UMCPBridge.IsRunning)
            {
                UMCPBridge.Start();
                // Wait a moment for the bridge to start
                yield return new WaitForSeconds(0.5f);
            }

            // Test each registered command handler in the CommandRegistry
            string[] commandHandlers = new string[]
            {
                "HandleManageScript",
                "HandleManageScene",
                "HandleManageEditor",
                "HandleManageGameObject", 
                "HandleManageAsset",
                "HandleReadConsole",
                "HandleExecuteMenuItem",
                "HandleGetProjectPath"
            };

            foreach (string handlerName in commandHandlers)
            {
                // Test getting the handler from CommandRegistry
                var handler = CommandRegistry.GetHandler(handlerName);
                Assert.IsNotNull(handler, $"Handler '{handlerName}' should be registered in CommandRegistry");

                // For each handler, create and send a command through the bridge to verify end-to-end functionality
                // We'll use a simple test with GetProjectPath since it requires minimal parameters
                if (handlerName == "HandleGetProjectPath")
                {
                    // Create a command for get_project_path command
                    Command command = new Command
                    {
                        type = "get_project_path",
                        @params = new JObject()
                    };

                    // Test the command using TCP client
                    yield return VerifyCommandExecutionViaUMCPBridge(command, (result) => { 
                    
                    
                    });
                    
                    // Test direct execution via handler
                    object result = handler(new JObject());
                    Assert.IsNotNull(result, "Direct handler execution should return a result");
                    Debug.Log($"Direct execution of {handlerName} result: {JsonConvert.SerializeObject(result)}");
                }
            }

            // Test non-existent handler
            var nonExistentHandler = CommandRegistry.GetHandler("NonExistentHandler");
            Assert.IsNull(nonExistentHandler, "Non-existent handler should return null");
        }

        private IEnumerator VerifyCommandExecutionViaUMCPBridge(Command command, System.Action<JObject> _onResult)
        {
            string commandJson = JsonConvert.SerializeObject(command);
            byte[] commandBytes = Encoding.UTF8.GetBytes(commandJson);

            using (TcpClient client = new TcpClient())
            {
                // Connect to the bridge
                var connectTask = ConnectToUMCPBridge(client);
                while (!connectTask.IsCompleted)
                    yield return null;

                if (!connectTask.Result)
                {
                    Assert.Fail("Failed to connect to UMCPBridge");
                    yield break;
                }

                using (NetworkStream stream = client.GetStream())
                {
                    // Send the command
                    stream.Write(commandBytes, 0, commandBytes.Length);

                    // Wait for response
                    byte[] buffer = new byte[8192];
                    var readTask = ReadFromStreamAsync(stream, buffer);
                    while (!readTask.IsCompleted)
                        yield return null;

                    int bytesRead = readTask.Result;
                    Assert.Greater(bytesRead, 0, "Should receive a non-empty response");

                    // Parse the response
                    string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    JObject response = JObject.Parse(responseJson);

                    // Verify response structure
                    Assert.IsNotNull(response["status"], "Response should contain a 'status' field");
                    Assert.AreEqual("success", response["status"].ToString(), "Expected status to be 'success'");
                    Assert.IsNotNull(response["result"], "Response should contain a 'result' field");

                    // Log the response for debugging
                    Debug.Log($"Command {command.type} response: {responseJson}");

                    if(_onResult != null)
                    {
                        _onResult(response["result"] as JObject);
                    }
                    
                }
            }
        }

        private async Task<bool> ConnectToUMCPBridge(TcpClient client)
        {
            try
            {
                await client.ConnectAsync("localhost", unityPort);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<int> ReadFromStreamAsync(NetworkStream stream, byte[] buffer)
        {
            try
            {
                return await stream.ReadAsync(buffer, 0, buffer.Length);
            }
            catch
            {
                return 0;
            }
        }
    }
}
