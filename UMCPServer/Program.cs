using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<EchoTool>();

await builder.Build().RunAsync();

[McpServerToolType]
public class EchoTool
{
    [McpServerTool, Description("Returns a greeting message from UMCPServer.")]
    public static string Echo()
    {
        return "Hello from UMCPServer A14655";
    }
}
