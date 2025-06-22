using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UMCPServer.Models;

public class UnityCommand
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonProperty("params")]
    public JObject? Params { get; set; }
}

public class UnityResponse
{
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonProperty("result")]
    public JObject? Result { get; set; }
    
    [JsonProperty("error")]
    public string? Error { get; set; }
    
    [JsonProperty("message")]
    public string? Message { get; set; }
}
