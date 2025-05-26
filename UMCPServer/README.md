# UMCP Server

A simple C# MCP (Model Context Protocol) server with an echo tool that returns "Hello from UMCPServer A14655".

## Features

- Single echo tool that returns a greeting message
- Docker support for easy deployment
- Compatible with Claude Desktop and other MCP clients

## Building and Running

### Local Development

1. Ensure you have .NET 9.0 SDK installed
2. Build the project:
   ```bash
   dotnet build
   ```
3. Run the server:
   ```bash
   dotnet run
   ```

### Docker Deployment

1. Build the Docker image:
   ```bash
   docker build -t umcpserver .
   ```

2. Run with Docker:
   ```bash
   docker run -it umcpserver
   ```

3. Or use Docker Compose:
   ```bash
   docker-compose up -d
   ```

## Usage with Claude Desktop

To use this server with Claude Desktop, add the following configuration to your Claude Desktop settings:

```json
{
  "mcpServers": {
    "umcp": {
      "command": "docker",
      "args": ["run", "-i", "umcpserver"]
    }
  }
}
```

## Tools Available

- **echo**: Returns "Hello from UMCPServer A14655"

## Project Structure

- `Program.cs` - Main entry point and tool implementation
- `UMCPServer.csproj` - Project file with dependencies
- `Dockerfile` - Docker configuration for containerization
- `docker-compose.yml` - Docker Compose configuration
- `.dockerignore` - Files to exclude from Docker build
