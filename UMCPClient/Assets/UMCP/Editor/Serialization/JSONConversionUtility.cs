using Newtonsoft.Json;
using UMCP.Editor.Helpers;
using UnityEngine;

namespace UMCP.Editor.Serialization
{
    public class JSONConversionUtility 
    {
        public static string SerializeObject(object? obj, bool prettyPrint = false)
        {
            //Debug.Log("Starting JSON serialization");
            return JsonConvert.SerializeObject(obj, Formatting.None, new JsonSerializerSettings()
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                Converters = { new Vector3Converter() },
            });
        }
    }
}
