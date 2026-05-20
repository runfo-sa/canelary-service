using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Client.Configuration
{
    /// <summary>
    /// Cifrado de secretos en reposo via Windows DPAPI con scope <see cref="DataProtectionScope.LocalMachine"/>.
    /// Cualquier proceso corriendo en la misma maquina con permisos suficientes puede descifrarlo;
    /// es aceptable para un servicio que corre como LocalSystem.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class SecretProtector
    {
        // Entropy adicional ata el ciphertext a esta aplicacion (defense-in-depth).
        private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("Canelary.Service.v1");

        public static string Protect(string plaintext)
        {
            byte[] data = Encoding.UTF8.GetBytes(plaintext);
            byte[] encrypted = ProtectedData.Protect(data, s_entropy, DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(encrypted);
        }

        public static string Unprotect(string base64)
        {
            byte[] encrypted = Convert.FromBase64String(base64);
            byte[] data = ProtectedData.Unprotect(encrypted, s_entropy, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(data);
        }
    }
}
