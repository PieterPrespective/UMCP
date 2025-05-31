using System;
using Newtonsoft.Json;

namespace UMCP.Editor.Models
{
    [Serializable]
    public class UMCPConfig
    {
        [JsonProperty("umcpServers")]
        public UMCPConfigServers umcpServers;
    }
}