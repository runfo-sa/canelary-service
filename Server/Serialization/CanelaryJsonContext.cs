using System.Text.Json.Serialization;
using Core;
using Server.Models;

namespace Server.Serialization
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Request))]
    [JsonSerializable(typeof(Etiqueta))]
    [JsonSerializable(typeof(Etiqueta[]))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(ClientStatus))]
    [JsonSerializable(typeof(List<ClientStatus>))]
    [JsonSerializable(typeof(EtiquetaCliente))]
    [JsonSerializable(typeof(List<EtiquetaCliente>))]
    [JsonSerializable(typeof(TipoDiff))]
    public partial class CanelaryJsonContext : JsonSerializerContext;
}
