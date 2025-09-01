using System;
using Newtonsoft.Json;
using UnityEngine;

namespace UMCP.Editor.Serialization
{
    /// <summary>
    /// JSON converter for Unity Vector3 type
    /// </summary>
    public class Vector3Converter : JsonConverter<Vector3>
    {
        /// <summary>
        /// Reads JSON and converts it to a Vector3
        /// </summary>
        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = serializer.Deserialize(reader);
            var iv = JsonConvert.DeserializeObject<Vector3>(t.ToString());
            return iv;
        }

        /// <summary>
        /// Writes a Vector3 to JSON
        /// </summary>
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            Debug.Log("Serializing Vector3: " + value);

            Vector3 v = (Vector3)value;
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(v.x);
            writer.WritePropertyName("y");
            writer.WriteValue(v.y);
            writer.WritePropertyName("z");
            writer.WriteValue(v.z);
            writer.WriteEndObject();
        }
    }
}