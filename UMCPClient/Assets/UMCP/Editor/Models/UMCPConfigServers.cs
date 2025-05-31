using System;
using Newtonsoft.Json;

namespace UMCP.Editor.Models
{
    [Serializable]
    public class UMCPConfigServers
    {
        [JsonProperty("umcp")]
        public UMCPConfigServer umcp;
    }
}