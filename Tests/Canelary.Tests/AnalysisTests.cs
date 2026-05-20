using System.Collections.Frozen;
using Core;
using Server.Logic;
using Server.Models;

namespace Canelary.Tests
{
    /// <summary>
    /// Analysis.CheckClient escribe logs por-cliente bajo CommonApplicationData (/usr/share en Linux,
    /// no escribible para el usuario del CI runner). Redirigimos via CANELARY_SERVER_LOG_BASE a una
    /// carpeta temporal por instancia de test class. xUnit corre los tests de una misma clase
    /// secuencialmente, asi que mutar el env var aca es seguro.
    /// </summary>
    public class AnalysisTests : IDisposable
    {
        private const string ClientName = "canelary-analysis-tests";
        private const string EnvVar = "CANELARY_SERVER_LOG_BASE";

        private readonly string _logBase;
        private readonly string? _previousEnvValue;

        public AnalysisTests()
        {
            _logBase = Path.Combine(Path.GetTempPath(), "canelary-analysis-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_logBase);
            _previousEnvValue = Environment.GetEnvironmentVariable(EnvVar);
            Environment.SetEnvironmentVariable(EnvVar, _logBase);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(EnvVar, _previousEnvValue);
            try { Directory.Delete(_logBase, recursive: true); } catch { }
        }

        private static FrozenSet<Etiqueta> Set(params Etiqueta[] items) => items.ToFrozenSet();

        private static Request Req(params Etiqueta[] items) => new(ClientName, items);

        [Fact]
        public void CheckClient_IdenticalSets_ReturnsOkay()
        {
            var server = Set(new Etiqueta("AAAA", "uno"), new Etiqueta("BBBB", "dos"));
            var req = Req(new Etiqueta("AAAA", "uno"), new Etiqueta("BBBB", "dos"));

            Assert.Equal(Status.Okay, Analysis.CheckClient(req, server).Status);
        }

        [Fact]
        public void CheckClient_ClientMissingEntry_ReturnsDesactualizada()
        {
            var server = Set(new Etiqueta("AAAA", "uno"), new Etiqueta("BBBB", "dos"));
            var req = Req(new Etiqueta("AAAA", "uno"));

            var (status, diff) = Analysis.CheckClient(req, server);

            Assert.Equal(Status.Desactualizada, status);
            Assert.Contains(diff, d => d.Nombre == "dos" && d.Tipo == TipoDiff.Desactualizada);
            Assert.DoesNotContain(diff, d => d.Tipo == TipoDiff.Sobrante);
        }

        [Fact]
        public void CheckClient_ClientHasExtraName_ReturnsSobrantes()
        {
            var server = Set(new Etiqueta("AAAA", "uno"));
            var req = Req(new Etiqueta("AAAA", "uno"), new Etiqueta("CCCC", "extra"));

            var (status, diff) = Analysis.CheckClient(req, server);

            Assert.Equal(Status.Sobrantes, status);
            Assert.Contains(diff, d => d.Nombre == "extra" && d.Tipo == TipoDiff.Sobrante);
            Assert.DoesNotContain(diff, d => d.Tipo == TipoDiff.Desactualizada);
        }

        [Fact]
        public void CheckClient_ClientHasExtraAndMissing_ReturnsDesactualizadaSobrantes()
        {
            var server = Set(new Etiqueta("AAAA", "uno"), new Etiqueta("BBBB", "dos"));
            var req = Req(new Etiqueta("AAAA", "uno"), new Etiqueta("CCCC", "extra"), new Etiqueta("DDDD", "otra"));

            var (status, diff) = Analysis.CheckClient(req, server);

            Assert.Equal(Status.DesactualizadaSobrantes, status);
            Assert.Contains(diff, d => d.Tipo == TipoDiff.Desactualizada);
            Assert.Contains(diff, d => d.Tipo == TipoDiff.Sobrante);
        }

        [Fact]
        public void CheckClient_SameCountButDifferentHash_ReturnsDesactualizada()
        {
            // Comportamiento documentado: sobrantes solo se computa si client.Count > server.Count.
            // Un "swap" mismo nombre + hash distinto se reporta como Desactualizada (no como Sobrante)
            // porque el comparer name-only no detecta el cambio de hash.
            var server = Set(new Etiqueta("AAAA", "uno"));
            var req = Req(new Etiqueta("XXXX", "uno"));

            Assert.Equal(Status.Desactualizada, Analysis.CheckClient(req, server).Status);
        }

        [Fact]
        public void CheckClient_Okay_DiffIsEmpty()
        {
            var server = Set(new Etiqueta("AAAA", "uno"), new Etiqueta("BBBB", "dos"));
            var req = Req(new Etiqueta("AAAA", "uno"), new Etiqueta("BBBB", "dos"));

            var (_, diff) = Analysis.CheckClient(req, server);

            Assert.Empty(diff);
        }

        [Fact]
        public void CheckClient_EmptyServerEmptyClient_ReturnsOkay()
        {
            var server = Set();
            var req = Req();

            Assert.Equal(Status.Okay, Analysis.CheckClient(req, server).Status);
        }
    }
}
