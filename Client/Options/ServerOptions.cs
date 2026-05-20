namespace Client.Options
{
    public sealed class ServerOptions
    {
        public const string SectionName = "Server";

        public required string Ip { get; init; }
        public required string Port { get; init; }

        /// <summary>
        /// "http" o "https". Default "http" para no romper clientes ya desplegados; el operador
        /// puede flipearlo a "https" en el JSON cuando el Server tenga su binding TLS listo.
        /// </summary>
        public string Scheme { get; init; } = "http";
    }
}
