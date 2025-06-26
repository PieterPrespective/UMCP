# ManageTestTool Integration Test

## Overview
This integration test validates the GetTests and RunTests MCP tools by simulating a complete test workflow in Unity.

## Test Scenario
The test follows these steps:
1. Validates Unity3D project is running with UMCP client active
2. Creates SimpleEditModeTests with three test methods
3. Forces Unity Editor recompilation
4. Tests all variations of GetTests tool parameters
5. Runs individual tests and validates results
6. Runs all tests and ensures failures are properly reported

## Running the Test

### Run All Integration Tests
```bash
dotnet test --filter "Category=Integration"
```

### Run Only ManageTestTool Integration Test
```bash
dotnet test --filter "FullyQualifiedName~ManageTestToolTests"
```

### Run with Detailed Output
```bash
dotnet test --filter "FullyQualifiedName~ManageTestToolTests" --logger "console;verbosity=detailed"
```

### From PowerShell Script
```powershell
./RunIntegrationTests.ps1
```

## Test Details

### SimpleEditModeTests
The test simulates creating these Unity tests:
- **TestAddition**: Asserts 1 + 1 equals 2 (passes)
- **TestSubstraction**: Asserts 5 - 1 equals 4 (passes)
- **TestFaulty**: Faultily asserts 6 + 2 equals 7 (fails)

### GetTests Variations Tested
1. TestMode = "All" - retrieves all tests
2. TestMode = "EditMode" - retrieves only EditMode tests
3. TestMode = "PlayMode" - retrieves only PlayMode tests
4. Filter = "SimpleEditMode" - filters tests by name

### RunTests Variations Tested
1. Run single test (TestAddition) with full output
2. Run single test without output
3. Run failing test (TestFaulty) and verify failure
4. Run all tests and verify mixed results

## Expected Behavior
- GetTests should return correct test lists based on mode and filter
- RunTests should properly execute tests and report results
- TestFaulty should always fail its assertion
- AllSuccess should be false when any test fails

## Dependencies
- Requires mocked Unity connection service
- Uses NUnit test framework
- Follows IEnumerator pattern for multi-step execution

## Real Unity Connection Tests

### Prerequisites
1. Unity Editor must be running with the UMCPClient project open
2. UMCP Bridge should be running (starts automatically)
3. Unity should be in EditMode (not PlayMode)

### Running Real Unity Tests
```bash
# Run all tests that require Unity
dotnet test --filter "Category=RequiresUnity"

# Run only the real Unity test tool tests
dotnet test --filter "FullyQualifiedName~ManageTestToolRealUnityTests"

# Run specific real Unity test
dotnet test --filter "FullyQualifiedName~ManageTestToolRealUnityTests.TestGetTestsWithRealUnity_ShouldNotLockEditor"
dotnet test --filter "FullyQualifiedName~ManageTestToolRealUnityTests.TestRunTestsWithRealUnity_ShouldNotLockEditor"
```

### What These Tests Validate
- GetTests and RunTests tools work with real Unity connection without locking the editor
- Proper async handling prevents Unity main thread blocking
- Test results are properly retrieved and processed