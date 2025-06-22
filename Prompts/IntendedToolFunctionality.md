## Manage Test Tools
The manage Test tool allows the MCP consumer to manage testing within the Unity3D Test runner
Generally, you will want to run all relevant tests after each (automated) development test step to regress continued compatibility

## Get Tests Tool
Get a list of all tests within the project, optionally with a name filter

### Input Params

| Param    | Type                                            | Description                                                                                                                       |
| -------- | ----------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| TestMode | string - either "EditMode", "PlayMode" or "All" | The mode of the tests to receive; either only tests runned in editmode, only tests runned in playmode or all tests                |
| Filter   | string                                          | If not empty, only return tests with the filter value in its namespace or name - if empty, return all tests matching the TestMode |

### Output Params
| Param           | Type   | Description                                                                                                                                                              |
| --------------- | ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| TestName        | string | The name of the test - note that this should follow the formatting required for invocation: either "MyTestClass.NameOfMyTest" or "SpecificTestFixture.NameOfAnotherTest" |
| TestAssembly    | string | The assembly this test belongs to                                                                                                                                        |
| TestNamespace   | string | The namespace of the test                                                                                                                                                |
| ContainerScript | string | The name of the script containing the test (can differ from the class name)                                                                                              |



## Run Tests Tool
Run the given tests in the Unity3D Testrunner - Invoking this tool automatically runs the "MarkStartOfNewStep" tool with a step formatted as "RunTests_[StringGUID]" - after completion of all tests 
### Input Params

| Param             | Type                                            | Description                                                                                                                                                                                                                               |
| ----------------- | ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| TestMode          | string - either "EditMode", "PlayMode" or "All" | The mode of the tests to run; either only tests runned in editmode, only tests runned in playmode or all tests                                                                                                                            |
| Filter            | Array of strings                                | If not empty, only run the tests provided (note that this should follow the formatting required for invocation: either "MyTestClass.NameOfMyTest" or "SpecificTestFixture.NameOfAnotherTest") - if empty; run all tests matching testmode |
| OutputTestResults | bool                                            | Whether to output TestResults                                                                                                                                                                                                             |
| OutputLogData     | bool                                            | Whether to output LogData                                                                                                                                                                                                                 |
### Output Params
| Param       | Type                    | Description                                                                 |
| ----------- | ----------------------- | --------------------------------------------------------------------------- |
| AllSuccess  | bool                    | Whether all runned tests were completed succesfully                         |
| TestResults | Array of TestResultData | Array with results of the completed tests                                   |
| LogData     | string                  | Result of the [RequestStepLogs] tool after all tests have completed running |
### TestResultData
| Param           | Type   | Description                                                                                                                                                              |
| --------------- | ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| TestName        | string | The name of the test - note that this should follow the formatting required for invocation: either "MyTestClass.NameOfMyTest" or "SpecificTestFixture.NameOfAnotherTest" |
| TestAssembly    | string | The assembly this test belongs to                                                                                                                                        |
| TestNamespace   | string | The namespace of the test                                                                                                                                                |
| ContainerScript | string | The name of the script containing the test (can differ from the class name)                                                                                              |
| Success         | bool   | whether the test completed succesfully                                                                                                                                   |