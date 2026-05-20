using Core;
using Microsoft.Extensions.Options;
using Server.Models;
using System.Collections.Frozen;

namespace Server.Logic
{
    /// <summary>
    /// Rescanea el directorio de Etiquetas a intervalos fijos y actualiza el
    /// <see cref="EtiquetasSnapshot"/> compartido.
    /// <para>
    /// Reemplaza al <c>FileSystemWatcher</c> original porque inotify (Linux) no propaga
    /// eventos sobre mounts CIFS, y porque <c>FileSystemWatcher</c> sobre UNC en Windows
    /// depende de notificaciones del servidor SMB que muchas veces no llegan.
    /// </para>
    /// <para>
    /// Optimizacion: antes de rehashar todos los archivos (lectura via SMB) calcula un
    /// <see cref="ComputeFingerprint"/> barato — solo nombre + tamaño + last-write-time —
    /// y skipea el full scan si no cambio.
    /// </para>
    /// </summary>
    public sealed class EtiquetasPollingService : BackgroundService
    {
        private readonly EtiquetasSnapshot _snapshot;
        private readonly string _path;
        private readonly TimeSpan _interval;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<EtiquetasPollingService> _logger;

        private long _lastFingerprint;
        private bool _hasFingerprint;

        // Contadores expuestos para tests (ver EtiquetasPollingServiceTests).
        internal int FullScanCount { get; private set; }
        internal int SkippedScanCount { get; private set; }

        public EtiquetasPollingService(
            EtiquetasSnapshot snapshot,
            IOptions<EtiquetasConfig> options,
            TimeProvider timeProvider,
            ILogger<EtiquetasPollingService> logger)
        {
            _snapshot = snapshot;
            _path = options.Value.Path;
            _interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollingIntervalSeconds));
            _timeProvider = timeProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Primer scan fuera del try/catch: si el share esta caido o el path mal configurado
            // queremos que el host crashee (BackgroundServiceExceptionBehavior.StopHost) y Docker
            // reinicie + el deploy falle loudly. Iteraciones subsiguientes son tolerantes.
            ScanOnce();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_interval, _timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) { return; }

                try
                {
                    ScanOnce();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falla transitoria escaneando {Path}, se reintenta en la proxima iteracion", _path);
                }
            }
        }

        internal bool ScanOnce()
        {
            long fingerprint = ComputeFingerprint(_path);
            if (_hasFingerprint && fingerprint == _lastFingerprint)
            {
                SkippedScanCount++;
                return false;
            }

            FrozenSet<Etiqueta> next = Scanner.GetEtiquetas(_path).ToFrozenSet();
            _snapshot.Replace(next);
            _lastFingerprint = fingerprint;
            _hasFingerprint = true;
            FullScanCount++;
            return true;
        }

        private static long ComputeFingerprint(string path)
        {
            HashCode hc = default;
            // EnumerateFiles devuelve un orden no garantizado entre filesystems; ordenamos
            // por nombre para que el fingerprint sea estable iteracion a iteracion.
            foreach (FileInfo fi in new DirectoryInfo(path).EnumerateFiles("*.e01").OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                hc.Add(fi.Name);
                hc.Add(fi.Length);
                hc.Add(fi.LastWriteTimeUtc.Ticks);
            }
            return hc.ToHashCode();
        }
    }
}
