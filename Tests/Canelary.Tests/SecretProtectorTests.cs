using System.Runtime.InteropServices;
using Client.Configuration;

namespace Canelary.Tests
{
    public class SecretProtectorTests
    {
        [Fact]
        public void Protect_RoundTrip_RecoversPlaintext()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return; // DPAPI solo existe en Windows.
            }

            const string plaintext = "QWERTY987-super-secret";
            string ciphertext = SecretProtector.Protect(plaintext);
            string recovered = SecretProtector.Unprotect(ciphertext);

            Assert.Equal(plaintext, recovered);
            Assert.NotEqual(plaintext, ciphertext);
        }

        [Fact]
        public void Protect_SamePlaintext_TwiceProducesDifferentCiphertext()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            string a = SecretProtector.Protect("ABC123");
            string b = SecretProtector.Protect("ABC123");
            // DPAPI mete entropia interna, asi que dos protecciones del mismo plaintext difieren.
            Assert.NotEqual(a, b);
            Assert.Equal("ABC123", SecretProtector.Unprotect(a));
            Assert.Equal("ABC123", SecretProtector.Unprotect(b));
        }
    }
}
