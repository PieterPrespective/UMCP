# UMCP Bridge Integration Tests

This directory contains integration tests that create a real UMCP Bridge connection between the server and Unity3D without any mocking.

## Test Files

### 1. UMCPBridgeIntegrationTest.cs
This test attempts to start Unity in headless/batch mode and connect to it automatically. It:
- Starts Unity with the UMCPClient project in headless mode
- Waits for Unity to initialize and the UMCP Bridge to start
- Connects to Unity via the UnityConnectionService
- Retrieves the project path
- Verifies the connection with additional commands

**Requirements:**
- Unity must be installed (the test will try to find it automatically)
- The test may need to be run with elevated permissions
- Unity path may need to be updated in the test constants

### 2. UMCPBridgeRealConnectionTest.cs
This is a more practical test that connects to an already running Unity instance. It:
- Checks if Unity is running with UMCP Client
- Connects to the running Unity instance
- Retrieves the project path
- Tests various commands to verify the bridge is working

**Requirements:**
- Unity Editor must be running
- The UMCPClient project must be open in Unity
- UMCP Bridge should be running (starts automatically when project opens)

## Running the Tests

### Option 1: Using Visual Studio
1. Open the UMCPServer.sln solution
2. Build the solution
3. Open Test Explorer (Test > Test Explorer)
4. Find the integration tests under the "Integration" category
5. Run the desired test

### Option 2: Using dotnet CLI
```bash
# Run all integration tests
dotnet test --filter Category=Integration

# Run only the real connection test (requires Unity running)
dotnet test --filter "FullyQualifiedName~UMCPBridgeRealConnectionTest"

# Run the headless Unity test
dotnet test --filter "FullyQualifiedName~UMCPBridgeIntegrationTest"
```

### Option 3: Using NUnit Console Runner
```bash
# Install NUnit Console if not already installed
dotnet tool install -g NUnit.ConsoleRunner.Net80

# Run the tests
nunit3-console UMCPServer.Tests.dll --where "cat == Integration"
```

## Setup Instructions for UMCPBridgeRealConnectionTest

1. Open Unity Editor
2. Open the project at: `C:\Prespective\250328_TestMLStuffUnity3d\UMCP\UMCPClient`
3. Wait for Unity to finish importing/compiling
4. The UMCP Bridge should start automatically (check Unity console for "UMCPBridge started on port 6400")
5. Run the `UMCPBridgeRealConnectionTest`

## Expected Output

When successful, the test will:
- Connect to Unity
- Retrieve and display the project path
- Show additional path information (data path, persistent data path, etc.)
- Verify the connection with a ping command
- Test editor state retrieval

Example output:
```
Step 1: Checking if Unity is running with UMCP Client...
Step 2: Connecting to Unity via UMCP Bridge...
Successfully connected to Unity!
Step 3: Getting project path from Unity...
Step 4: Verifying project path result...
Retrieved project path: C:\Prespective\250328_TestMLStuffUnity3d\UMCP\UMCPClient
Data path: C:\Prespective\250328_TestMLStuffUnity3d\UMCP\UMCPClient\Assets
Persistent data path: C:\Users\[Username]\AppData\LocalLow\DefaultCompany\UMCPClient
...
Integration test completed successfully!
```

## Troubleshooting

### Unity Not Found (UMCPBridgeIntegrationTest)
- Update the `UnityExecutablePath` constant in the test
- Ensure Unity is installed via Unity Hub
- The test looks in common installation directories

### Connection Failed
- Ensure Unity is running with the UMCPClient project open
- Check that port 6400 is not blocked by firewall
- Verify UMCP Bridge started in Unity console
- Check Unity console for any error messages

### Test Timeout
- Increase timeout values in the test
- Check if Unity is responding normally
- Verify no other process is using port 6400

## Notes

- The integration tests use real network connections and file system operations
- No mocking is used - these are true integration tests
- Tests may take longer to run due to Unity startup time (for headless test)
- The `RequiresUnity` category can be used to skip tests when Unity is not available
