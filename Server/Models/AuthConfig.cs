namespace Server.Models
{
    public class AuthConfig
    {
        public required string ClavePublica { get; set; }
        public required string ClavePrivada { get; set; }
        public required string ClaveDescarga { get; set; }
    }
}