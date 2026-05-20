using Core;
using Server.Logic;

namespace Canelary.Tests
{
    public class AuthValidatorTests
    {
        private const string PublicKey = "ABC123";
        private const string PrivateKey = "QWERTY987";
        private static readonly string ValidHash = Encryption.EncryptKey(PublicKey, PrivateKey);

        [Fact]
        public void IsRequestAuthorized_ValidKeyAndHash_ReturnsTrue()
        {
            Assert.True(AuthValidator.IsRequestAuthorized(PublicKey, ValidHash, PrivateKey));
        }

        [Fact]
        public void IsRequestAuthorized_WrongPrivateKey_ReturnsFalse()
        {
            Assert.False(AuthValidator.IsRequestAuthorized(PublicKey, ValidHash, "otra-clave-privada"));
        }

        [Fact]
        public void IsRequestAuthorized_TamperedHash_ReturnsFalse()
        {
            // Cambiamos un solo char del hash valido y debe rechazar.
            string tampered = ValidHash[..^1] + (ValidHash[^1] == 'A' ? 'B' : 'A');
            Assert.False(AuthValidator.IsRequestAuthorized(PublicKey, tampered, PrivateKey));
        }

        [Theory]
        [InlineData(null, "h")]
        [InlineData("", "h")]
        [InlineData("k", null)]
        [InlineData("k", "")]
        [InlineData(null, null)]
        public void IsRequestAuthorized_MissingKeyOrHash_ReturnsFalse(string? key, string? hash)
        {
            Assert.False(AuthValidator.IsRequestAuthorized(key, hash, PrivateKey));
        }

        [Fact]
        public void IsRequestAuthorized_HashWithDifferentLength_ReturnsFalse()
        {
            // Una longitud incorrecta debe rechazar sin tirar excepciones.
            Assert.False(AuthValidator.IsRequestAuthorized(PublicKey, "muy-corto", PrivateKey));
            Assert.False(AuthValidator.IsRequestAuthorized(PublicKey, ValidHash + "extra", PrivateKey));
        }

        [Fact]
        public void IsDownloadKeyValid_MatchingKey_ReturnsTrue()
        {
            Assert.True(AuthValidator.IsDownloadKeyValid("ABC123", "ABC123"));
        }

        [Fact]
        public void IsDownloadKeyValid_DifferentKey_ReturnsFalse()
        {
            Assert.False(AuthValidator.IsDownloadKeyValid("XYZ", "ABC123"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void IsDownloadKeyValid_MissingKey_ReturnsFalse(string? receivedKey)
        {
            Assert.False(AuthValidator.IsDownloadKeyValid(receivedKey, "ABC123"));
        }

        [Fact]
        public void IsDownloadKeyValid_DifferentLength_ReturnsFalse()
        {
            // FixedTimeEquals exige longitudes iguales; el helper lo cortocircuita correctamente.
            Assert.False(AuthValidator.IsDownloadKeyValid("ABC1234", "ABC123"));
            Assert.False(AuthValidator.IsDownloadKeyValid("ABC12", "ABC123"));
        }
    }
}
