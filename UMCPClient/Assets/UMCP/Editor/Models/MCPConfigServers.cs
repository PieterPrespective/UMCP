using System;
using Newtonsoft.Json;

namespace UMCP.Editor.Models
{
    [Serializable]
    public class MCPConfigServers
    {
        [JsonProperty("unityMCP")]
        public MCPConfigServer unityMCP;
    }
}