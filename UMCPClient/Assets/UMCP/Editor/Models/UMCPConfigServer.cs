using System;
using Newtonsoft.Json;

namespace UMCP.Editor.Models
{
    [Serializable]
    public class UMCPConfigServer
    {
        [JsonProperty("command")]
        public string command;

        [JsonProperty("args")]
        public string[] args;
    }
}