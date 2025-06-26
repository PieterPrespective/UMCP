Please read the document 'Prompts/AssignmentVariables.md', 'Prompts/IntendedToolFunctionality.md', 'Prompts/SoftwareArchitectureRules.md', 'Prompts/ToolIntegrationTests.md', 'Prompts/ToolRules.md' and 'PromptsWorkLogging.md'
- The tool is already implemented in both the client and server (So please read the database for context), 
- Running the tool GetTests without any filter (so it should return all tests) via claude desktop code using MCP returns no tests where the client project contains 78
- The following warning is displayed in the client project: [GetTests] Synchronous operation timed out after 15 seconds
UnityEngine.Debug:LogWarning (object)
UMCP.Editor.Tools.GetTestsUtility:GetTestsByParameters (UMCP.Editor.Tools.GetTestsParameters) (at Assets/UMCP/Editor/Tools/GetTests.cs:154)
UMCP.Editor.Tools.GetTests:HandleCommand (Newtonsoft.Json.Linq.JObject) (at Assets/UMCP/Editor/Tools/GetTests.cs:260)
UMCP.Editor.UMCPBridge:ExecuteCommand (UMCP.Editor.Models.Command) (at Assets/UMCP/Editor/UMCPBridge.cs:437)
UMCP.Editor.UMCPBridge:ProcessCommands () (at Assets/UMCP/Editor/UMCPBridge.cs:338)
UnityEditor.EditorApplication:Internal_CallUpdateFunctions ()