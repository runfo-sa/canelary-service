using System.Security.Cryptography;
using System.Text;
using Core;

namespace Canelary.Tests
{
    public class EncryptionTests
    {
        [Fact]
        public void EncryptKey_IsDeterministic()
        {
            string a = Encryption.EncryptKey("ABC123", "QWERTY987");
            string b = Encryption.EncryptKey("ABC123", "QWERTY987");
            Assert.Equal(a, b);
        }

        [Fact]
        public void EncryptKey_MatchesRawHmacSha256Base64()
        {
            const string publicKey = "ABC123";
            const string privateKey = "QWERTY987";

            byte[] expected = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(privateKey),
                Encoding.UTF8.GetBytes(publicKey));
            string expectedBase64 = Convert.ToBase64String(expected);

            string actual = Encryption.EncryptKey(publicKey, privateKey);
            Assert.Equal(expectedBase64, actual);
        }

        [Theory]
        [InlineData("a", "b")]
        [InlineData("ABC123", "DIFFERENT")]
        [InlineData("X", "QWERTY987")]
        public void EncryptKey_DifferentInputs_ProduceDifferentOutputs(string publicKey, string privateKey)
        {
            string baseline = Encryption.EncryptKey("ABC123", "QWERTY987");
            string other = Encryption.EncryptKey(publicKey, privateKey);
            Assert.NotEqual(baseline, other);
        }

    }
}
