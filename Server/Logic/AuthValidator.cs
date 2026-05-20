using System.Security.Cryptography;
using System.Text;
using Core;

namespace Server.Logic
{
    /// <summary>
    /// Helpers de autenticacion centralizados. Toda comparacion sensible usa
    /// <see cref="CryptographicOperations.FixedTimeEquals"/> para evitar timing leaks.
    /// </summary>
    public static class AuthValidator
    {
        /// <summary>
        /// Valida que el cliente haya firmado el header <c>request-key</c> con la clave privada
        /// configurada en el server, obteniendo el mismo hash que envia en <c>request-hash</c>.
        /// </summary>
        public static bool IsRequestAuthorized(string? receivedKey, string? receivedHash, string privateKey)
        {
            if (string.IsNullOrEmpty(receivedKey) || string.IsNullOrEmpty(receivedHash))
            {
                return false;
            }

            string expectedHash = Encryption.EncryptKey(receivedKey, privateKey);
            return ConstantTimeEquals(expectedHash, receivedHash);
        }

        /// <summary>
        /// Compara la clave de descarga recibida contra la esperada en tiempo constante.
        /// </summary>
        public static bool IsDownloadKeyValid(string? receivedKey, string expectedKey)
        {
            if (string.IsNullOrEmpty(receivedKey))
            {
                return false;
            }
            return ConstantTimeEquals(receivedKey, expectedKey);
        }

        private static bool ConstantTimeEquals(string a, string b)
        {
            // Las longitudes se comparan en cleartext (FixedTimeEquals exige iguales); diferencia
            // de longitud es de facto un fail pero no leak un secret aprovechable.
            byte[] aBytes = Encoding.UTF8.GetBytes(a);
            byte[] bBytes = Encoding.UTF8.GetBytes(b);
            if (aBytes.Length != bBytes.Length)
            {
                return false;
            }
            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}
