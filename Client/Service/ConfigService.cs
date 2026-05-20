using System.Runtime.Versioning;
using System.Text.Json;
using Client.Configuration;

namespace Client.Service
{
    [SupportedOSPlatform("windows")]
    public class ConfigService
    {
        private const string DEFAULT_CONFIG = """
            {
              "Server": {
                "Ip": "localhost",
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
              },
              "Auth": {
                "ClavePublica": "ABC123",
                "ClavePrivada": "QWERTY987",
                "ClaveDescarga": "ABC123"
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
            bool needsMigration;
            if (Path.Exists(_configPath))
            {
                string configDump = File.ReadAllText(_configPath);
                Data = JsonSerializer.Deserialize<ConfigModel>(configDump, _jsonOptions)
                    ?? throw new InvalidOperationException($"Failed to deserialize config from '{_configPath}'.");
                // Si el archivo en disco esta encriptado, lo desciframos a memoria.
                // Si esta en plaintext, lo dejamos en memoria pero forzamos un re-save encriptado.
                needsMigration = !DecryptAuthInPlace(Data.Auth);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
                Data = JsonSerializer.Deserialize<ConfigModel>(DEFAULT_CONFIG, _jsonOptions)!;
                needsMigration = true;
            }

            if (needsMigration)
            {
                Save();
            }
        }

        public void Save()
        {
            // Clonamos los campos sensibles para no mutar el objeto en memoria al encriptar.
            ConfigModel snapshot = Clone(Data);
            EncryptAuthInPlace(snapshot.Auth);
            string configDump = JsonSerializer.Serialize(snapshot, _jsonOptions);
            File.WriteAllText(_configPath, configDump);
        }

        /// <summary>
        /// Si <paramref name="auth"/> indica <c>Encrypted=true</c>, descifra las tres claves in-place
        /// y vuelve a marcar <c>Encrypted=false</c>. Devuelve <c>true</c> si el archivo ya estaba
        /// encriptado y <c>false</c> si era plaintext (=> hay que migrar).
        /// </summary>
        private static bool DecryptAuthInPlace(Auth? auth)
        {
            if (auth is null || !auth.Encrypted)
            {
                return false;
            }
            auth.ClavePublica = SecretProtector.Unprotect(auth.ClavePublica);
            auth.ClavePrivada = SecretProtector.Unprotect(auth.ClavePrivada);
            auth.ClaveDescarga = SecretProtector.Unprotect(auth.ClaveDescarga);
            auth.Encrypted = false;
            return true;
        }

        private static void EncryptAuthInPlace(Auth? auth)
        {
            if (auth is null || auth.Encrypted)
            {
                return;
            }
            auth.ClavePublica = SecretProtector.Protect(auth.ClavePublica);
            auth.ClavePrivada = SecretProtector.Protect(auth.ClavePrivada);
            auth.ClaveDescarga = SecretProtector.Protect(auth.ClaveDescarga);
            auth.Encrypted = true;
        }

        private static ConfigModel Clone(ConfigModel src) => new()
        {
            Server = src.Server is null ? null : new Server { Ip = src.Server.Ip, Port = src.Server.Port, Scheme = src.Server.Scheme },
            App = src.App is null ? null : new App
            {
                Unidad = src.App.Unidad,
                UpdateTime = src.App.UpdateTime,
                PiquatroTime = src.App.PiquatroTime,
                IntervaloMins = src.App.IntervaloMins,
                PiPath = src.App.PiPath,
            },
            Auth = src.Auth is null ? null : new Auth
            {
                ClavePublica = src.Auth.ClavePublica,
                ClavePrivada = src.Auth.ClavePrivada,
                ClaveDescarga = src.Auth.ClaveDescarga,
                Encrypted = src.Auth.Encrypted,
            },
        };
    }

    public class ConfigModel
    {
        public Server? Server { get; set; }
        public App? App { get; set; }
        public Auth? Auth { get; set; }
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

    public class Auth
    {
        public string ClavePublica { get; set; } = "";
        public string ClavePrivada { get; set; } = "";
        public string ClaveDescarga { get; set; } = "";
        /// <summary>
        /// Si <c>true</c>, los tres campos contienen ciphertext base64 producido por DPAPI
        /// (<see cref="SecretProtector"/>). En memoria siempre se mantiene plaintext.
        /// </summary>
        public bool Encrypted { get; set; }
    }
}
