using System;
using Newtonsoft.Json;

namespace UMCP.Editor.Models
{
    [Serializable]
    public class MCPConfig
    {
        [JsonProperty("mcpServers")]
        public MCPConfigServers mcpServers;
    }
}