using System.Diagnostics;
using System.Security.Cryptography;
using Client.Options;
using Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Client.Service
{
    /// <summary>
    /// Orquesta la logica del cliente: descubrir PiQuatro, enviar etiquetas, auto-actualizarse.
    /// El transporte HTTP esta delegado a <see cref="ICanelaryApi"/>.
    /// </summary>
    public sealed class ClientService(
        ICanelaryApi api,
        ConfigService config,
        IOptions<AuthOptions> authOptions,
        ILogger<ClientService> logger)
    {
        private readonly string _ip = Network.GetIpAddress();
        private readonly AuthOptions _auth = authOptions.Value;
        private string[] _foundInstallations = [];

        public async Task EnsurePiPathAsync(CancellationToken cancellationToken = default)
        {
            if (config.Data.App!.PiPath is not null)
            {
                return;
            }

            try
            {
                await CheckPiQuatroAsync(cancellationToken);
            }
            catch (MultipleInstalls) { /* Ya reportado dentro de CheckPiQuatroAsync. */ }
            catch (NoInstallsFound) { /* Ya reportado dentro de CheckPiQuatroAsync. */ }
        }

        public async Task SendEtiquetas(CancellationToken cancellationToken = default)
        {
            Etiqueta[] etiquetas = Scanner.GetEtiquetas(config.Data.App!.PiPath!);
            await api.ValidateClientAsync(new Request(_ip, etiquetas), cancellationToken);
        }

        public Task SendMultipleInstalls(CancellationToken cancellationToken = default)
        {
            string msg = string.Join(Environment.NewLine, _foundInstallations);
            return api.ReportMultipleInstallationsAsync(new Request(_ip, Etiquetas: null, Message: msg), cancellationToken);
        }

        public Task SendNoInstalls(CancellationToken cancellationToken = default)
        {
            return api.ReportNotInstalledAsync(new Request(_ip, Etiquetas: null), cancellationToken);
        }

        public async Task CheckPiQuatroAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string piPath = FindPiQuatro(config.Data.App!.Unidad);
                config.Data.App!.PiPath = piPath;
                config.Save();
            }
            catch (MultipleInstalls)
            {
                await SendMultipleInstalls(cancellationToken);
                throw;
            }
            catch (NoInstallsFound)
            {
                await SendNoInstalls(cancellationToken);
                throw;
            }
        }

        /// <summary>
        /// Busca la instalación de <b>PiQuatro</b> en la <paramref name="unidad"/> de disco especificada.
        /// </summary>
        /// <exception cref="NoInstallsFound"/>
        /// <exception cref="MultipleInstalls"/>
        private string FindPiQuatro(string unidad)
        {
            DateTime cutoff = DateTime.Now.AddYears(-1);
            _foundInstallations = Array.FindAll(
                Directory.GetFiles(unidad + "\\", "PiQuatro.exe", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                }),
                f => File.GetLastWriteTime(f) > cutoff && !f.Contains("test", StringComparison.CurrentCultureIgnoreCase)
            );

            return _foundInstallations.Length switch
            {
                0 => throw new NoInstallsFound(),
                1 => Directory.GetParent(_foundInstallations[0])!.FullName + "\\Etiquetas",
                _ => throw new MultipleInstalls(_foundInstallations),
            };
        }

        public async Task GetUpdate(CancellationToken cancellationToken = default)
        {
            try
            {
                string serverHash = (await api.GetClientVersionAsync(cancellationToken)).Trim('"');

                string clientExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Client.exe");
                byte[] localHashBytes;
                await using (var clientStream = File.OpenRead(clientExePath))
                {
                    localHashBytes = await SHA256.HashDataAsync(clientStream, cancellationToken);
                }
                string localHash = Scanner.GetHashString(localHashBytes);

                if (localHash == serverHash)
                {
                    return;
                }

                using HttpResponseMessage response = await api.DownloadInstallerAsync(_auth.ClaveDescarga, cancellationToken);
                response.EnsureSuccessStatusCode();

                string filename = response.Content.Headers.ContentDisposition!.FileName!;
                string path = Path.Combine(Path.GetTempPath(), "VSTCTemp");
                string filepath = Path.Combine(path, filename);
                Directory.CreateDirectory(path);

                await using (var fs = new FileStream(filepath, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs, cancellationToken);
                }

                using var process = new Process();
                process.StartInfo = new ProcessStartInfo("powershell.exe", $"-ExecutionPolicy Bypass -File \"{filepath}\"");
                process.Start();

                logger.LogInformation("Actualizacion descargada, iniciando installer.ps1 y terminando proceso");
                Environment.Exit(0);
            }
            catch (Exception err)
            {
                logger.LogError(err, "Fallo durante GetUpdate");
            }
        }

        private sealed class NoInstallsFound() : Exception("No se encontro ninguna instalación de PiQuatro");

        private sealed class MultipleInstalls(string[] paths) : Exception("Se encontraron mas de una instalación de PiQuatro")
        {
            public string[] Paths { get; } = paths;
        }
    }
}
