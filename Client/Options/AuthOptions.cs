namespace Client.Options
{
    public sealed class AuthOptions
    {
        public const string SectionName = "Auth";

        public required string ClavePublica { get; init; }
        public required string ClavePrivada { get; init; }
        public required string ClaveDescarga { get; init; }
    }
}
