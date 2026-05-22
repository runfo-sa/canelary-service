using System.Runtime.Versioning;
using System.Text.Json;

namespace Client.Service
{
    [SupportedOSPlatform("windows")]
    public class ConfigService
    {
        private const string DEFAULT_CONFIG = """
            {
              "Server": {
                "Ip": "canelary.runfosa.local",
                "Port": "5262"
              },
              "App": {
                // Unidad en la que se busca la instalacion de PiQuatro
                "Unidad": "C:",
                // Hora del dia en la que se actualizara el servicio (formato 24hs)
                "UpdateTime": 0,
                // Hora del dia en la que analiza por multiples instalaciones de PiQuatro (formato 24hs)
                "PiquatroTime": 2,
                // Tiempo (en minutos) para enviar las etiquetas al servidor
                "IntervaloMins": 5,
                "PiPath": null
              }
            }
            """;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private readonly string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Canelary Service\\appsettings.json"
        );

        public ConfigModel Data { get; set; }

        public ConfigService()
        {
            if (Path.Exists(_configPath))
            {
                string configDump = File.ReadAllText(_configPath);
                Data = JsonSerializer.Deserialize<ConfigModel>(configDump, _jsonOptions)
                    ?? throw new InvalidOperationException($"Failed to deserialize config from '{_configPath}'.");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
                Data = JsonSerializer.Deserialize<ConfigModel>(DEFAULT_CONFIG, _jsonOptions)!;
                Save();
            }
        }

        public void Save()
        {
            string configDump = JsonSerializer.Serialize(Data, _jsonOptions);
            File.WriteAllText(_configPath, configDump);
        }
    }

    public class ConfigModel
    {
        public Server? Server { get; set; }
        public App? App { get; set; }
    }

    public class Server
    {
        public string Ip { get; set; } = "";
        public string Port { get; set; } = "";
        /// <summary>Scheme HTTP a usar: "http" o "https". Null/vacio => "http" (default).</summary>
        public string? Scheme { get; set; }
    }

    public class App
    {
        public string Unidad { get; set; } = "";
        public int UpdateTime { get; set; }
        public int PiquatroTime { get; set; }
        public int IntervaloMins { get; set; }
        public string? PiPath { get; set; }
    }
}
