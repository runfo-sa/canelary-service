using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Server.Logic;
using Server.Models;

namespace Canelary.Tests
{
    public class EtiquetasPollingServiceTests : IDisposable
    {
        private readonly string _dir;

        public EtiquetasPollingServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "canelary-polling-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        private (EtiquetasPollingService service, EtiquetasSnapshot snapshot) Build(string? overridePath = null)
        {
            var snapshot = new EtiquetasSnapshot();
            var options = Options.Create(new EtiquetasConfig
            {
                Path = overridePath ?? _dir,
                PollingIntervalSeconds = 30,
            });
            var service = new EtiquetasPollingService(
                snapshot,
                options,
                TimeProvider.System,
                NullLogger<EtiquetasPollingService>.Instance);
            return (service, snapshot);
        }

        [Fact]
        public async Task ExecuteAsync_NonexistentPath_FaultsExecuteTaskForStopHost()
        {
            // Fail-fast: si el share esta caido o el path es invalido, ExecuteAsync debe
            // dejar el Task en estado faulted sin handlearlo. El host detecta el fault via
            // BackgroundServiceExceptionBehavior.StopHost (default en .NET 6+) y crashea el
            // contenedor; Docker lo reinicia y el deploy falla loudly.
            //
            // BackgroundService.StartAsync no propaga el throw al caller en .NET 10; la
            // excepcion queda en ExecuteTask. Awaitarlo expone el fault.
            string bogus = Path.Combine(_dir, "no-existe-" + Guid.NewGuid().ToString("N"));
            (var service, _) = Build(bogus);

            await service.StartAsync(CancellationToken.None);
            Assert.NotNull(service.ExecuteTask);
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.ExecuteTask!);
        }

        [Fact]
        public void ScanOnce_NonexistentPath_ThrowsDirectoryNotFound()
        {
            string bogus = Path.Combine(_dir, "no-existe-" + Guid.NewGuid().ToString("N"));
            (var service, _) = Build(bogus);

            Assert.Throws<DirectoryNotFoundException>(() => service.ScanOnce());
        }

        [Fact]
        public void ScanOnce_PopulatesSnapshotWithE01Files()
        {
            File.WriteAllText(Path.Combine(_dir, "uno.e01"), "x");
            File.WriteAllText(Path.Combine(_dir, "dos.e01"), "y");
            File.WriteAllText(Path.Combine(_dir, "ignorado.txt"), "z");

            (var service, var snapshot) = Build();
            Assert.True(service.ScanOnce());

            Assert.Equal(2, snapshot.Current.Count);
            Assert.Contains(snapshot.Current, e => e.Name == "uno");
            Assert.Contains(snapshot.Current, e => e.Name == "dos");
        }

        [Fact]
        public void ScanOnce_DetectsNewFile()
        {
            File.WriteAllText(Path.Combine(_dir, "inicial.e01"), "x");
            (var service, var snapshot) = Build();
            service.ScanOnce();
            Assert.Single(snapshot.Current);

            File.WriteAllText(Path.Combine(_dir, "nuevo.e01"), "y");
            Assert.True(service.ScanOnce());

            Assert.Equal(2, snapshot.Current.Count);
        }

        [Fact]
        public void ScanOnce_DetectsRemovedFile()
        {
            string toRemove = Path.Combine(_dir, "vence.e01");
            File.WriteAllText(toRemove, "x");
            File.WriteAllText(Path.Combine(_dir, "queda.e01"), "y");

            (var service, var snapshot) = Build();
            service.ScanOnce();
            Assert.Equal(2, snapshot.Current.Count);

            File.Delete(toRemove);
            Assert.True(service.ScanOnce());

            Assert.Single(snapshot.Current);
            Assert.Equal("queda", snapshot.Current.First().Name);
        }

        [Fact]
        public void ScanOnce_DetectsContentChange()
        {
            string filePath = Path.Combine(_dir, "cambia.e01");
            File.WriteAllBytes(filePath, [0x01]);

            (var service, var snapshot) = Build();
            service.ScanOnce();
            string hashBefore = snapshot.Current.First().Hash;

            // Cambiar tamaño asegura que el fingerprint difiera incluso si LWT tick coincide.
            File.WriteAllBytes(filePath, [0x01, 0x02, 0x03, 0x04]);
            Assert.True(service.ScanOnce());

            string hashAfter = snapshot.Current.First().Hash;
            Assert.NotEqual(hashBefore, hashAfter);
        }

        [Fact]
        public void ScanOnce_SkipsFullScanWhenFingerprintUnchanged()
        {
            File.WriteAllText(Path.Combine(_dir, "estable.e01"), "x");
            (var service, _) = Build();

            Assert.True(service.ScanOnce());
            Assert.False(service.ScanOnce());

            Assert.Equal(1, service.FullScanCount);
            Assert.Equal(1, service.SkippedScanCount);
        }

        [Fact]
        public void ScanOnce_RecoversAfterTransientFailure()
        {
            // Simula una caida transitoria del share borrando el directorio entre scans:
            // la 2da invocacion tira IOException/DirectoryNotFoundException, pero en el loop
            // real la excepcion la atrapa el try/catch y la 3ra invocacion (con el share
            // restaurado) actualiza el snapshot.
            File.WriteAllText(Path.Combine(_dir, "antes.e01"), "x");
            (var service, var snapshot) = Build();
            service.ScanOnce();
            Assert.Single(snapshot.Current);

            Directory.Delete(_dir, recursive: true);
            Assert.ThrowsAny<Exception>(() => service.ScanOnce());

            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "despues.e01"), "y");
            Assert.True(service.ScanOnce());

            Assert.Single(snapshot.Current);
            Assert.Equal("despues", snapshot.Current.First().Name);
        }
    }
}
