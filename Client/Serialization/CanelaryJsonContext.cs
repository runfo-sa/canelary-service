using System.Text.Json;
using System.Text.Json.Serialization;
using Core;

namespace Client.Serialization
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Request))]
    [JsonSerializable(typeof(Etiqueta))]
    [JsonSerializable(typeof(Etiqueta[]))]
    [JsonSerializable(typeof(string))]
    public partial class CanelaryJsonContext : JsonSerializerContext;
}
