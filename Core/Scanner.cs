using System.Security.Cryptography;

namespace Core
{
    public static class Scanner
    {
        /// <summary>
        /// Escanea <paramref name="path"/> en busca de archivos de etiquetas (con extension '.e01')
        /// </summary>
        /// <param name="path">Dirección a escanear</param>
        public static Etiqueta[] GetEtiquetas(string path)
        {
            string[] files = Directory.GetFiles(path, "*.e01", new EnumerationOptions
            {
                MatchCasing = MatchCasing.CaseInsensitive,
            });
            Etiqueta[] etiquetas = new Etiqueta[files.Length];

            for (int i = 0; i < files.Length; i++)
            {
                string f = files[i];

                string name = Path.GetFileNameWithoutExtension(f).ToLower();
                using FileStream fs = File.OpenRead(f);
                string hash = GetHashString(SHA256.HashData(fs));

                etiquetas[i] = new Etiqueta(hash, name);
            }

            return etiquetas;
        }

        /// <summary>
        /// Convierte una array de <see cref="byte"/> en una <see cref="string"/> hexadecimal
        /// </summary>
        public static string GetHashString(byte[] bytes) => Convert.ToHexString(bytes);
    }
}
