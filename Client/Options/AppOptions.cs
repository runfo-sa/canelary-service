namespace Client.Options
{
    public sealed class AppOptions
    {
        public const string SectionName = "App";

        public required string Unidad { get; init; }
        public int UpdateTime { get; init; }
        public int PiquatroTime { get; init; }
        public int IntervaloMins { get; init; }
        public string? PiPath { get; init; }
    }
}
