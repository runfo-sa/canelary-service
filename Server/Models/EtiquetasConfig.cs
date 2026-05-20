namespace Server.Models
{
    public class EtiquetasConfig
    {
        public required string Path { get; set; }

        public int PollingIntervalSeconds { get; set; } = 30;
    }
}
