using System.Security.Cryptography;
using System.Text;
using Core;

namespace Canelary.Tests
{
    public class ScannerTests : IDisposable
    {
        private readonly string _dir;

        public ScannerTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "canelary-scanner-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        [Fact]
        public void GetHashString_ProducesUppercaseHex()
        {
            byte[] bytes = [0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0xFF];
            string hex = Scanner.GetHashString(bytes);
            Assert.Equal("DEADBEEF00FF", hex);
        }

        [Fact]
        public void GetEtiquetas_ReturnsExpectedHashAndLowercaseName()
        {
            byte[] payload = Encoding.UTF8.GetBytes("contenido de etiqueta de prueba");
            string filePath = Path.Combine(_dir, "EtiquetaUno.e01");
            File.WriteAllBytes(filePath, payload);

            byte[] expectedBytes = SHA256.HashData(payload);
            string expectedHash = Scanner.GetHashString(expectedBytes);

            Etiqueta[] result = Scanner.GetEtiquetas(_dir);
            Etiqueta etiqueta = Assert.Single(result);
            Assert.Equal("etiquetauno", etiqueta.Name);
            Assert.Equal(expectedHash, etiqueta.Hash);
        }

        [Fact]
        public void GetEtiquetas_IgnoresNonE01Files()
        {
            File.WriteAllText(Path.Combine(_dir, "valida.e01"), "x");
            File.WriteAllText(Path.Combine(_dir, "invalida.txt"), "y");
            File.WriteAllText(Path.Combine(_dir, "tambien.e02"), "z");

            Etiqueta[] result = Scanner.GetEtiquetas(_dir);
            Assert.Single(result);
            Assert.Equal("valida", result[0].Name);
        }

        [Fact]
        public void GetEtiquetas_EmptyDirectory_ReturnsEmptyArray()
        {
            Etiqueta[] result = Scanner.GetEtiquetas(_dir);
            Assert.Empty(result);
        }

        [Fact]
        public void GetEtiquetas_ParallelInvocations_AreThreadSafe()
        {
            // Cementa la fix de Phase 1: el SHA256 estatico/compartido fue reemplazado por
            // SHA256.HashData(stream) thread-safe. Sin la fix, este test podia tirar
            // CryptographicException o devolver hashes corruptos.
            for (int i = 0; i < 10; i++)
            {
                byte[] bytes = Encoding.UTF8.GetBytes($"contenido-{i}");
                File.WriteAllBytes(Path.Combine(_dir, $"etiqueta{i}.e01"), bytes);
            }

            var hashes = new System.Collections.Concurrent.ConcurrentBag<string>();
            Parallel.For(0, 100, _ =>
            {
                Etiqueta[] result = Scanner.GetEtiquetas(_dir);
                foreach (var et in result)
                {
                    hashes.Add(et.Name + ":" + et.Hash);
                }
            });

            // 100 invocaciones, 10 archivos cada una -> 1000 entradas, todas iguales si
            // no hubo corrupcion de estado compartido.
            Assert.Equal(1000, hashes.Count);
            int distinct = hashes.Distinct().Count();
            Assert.Equal(10, distinct);
        }
    }
}
