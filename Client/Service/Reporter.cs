using Core;

namespace Client.Service
{
    public static class Reporter
    {
        private static readonly string FOLDER_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Canelary Service\\Logs\\"
        );

        /// <summary>
        /// Escribe un <paramref name="msg"/> atravez del <see cref="Logger"/>, en la dirección AppData\CanelaryService.
        /// <br/>
        /// Abre el explorador de archivos con el log seleccionado.
        /// </summary>
        public static void ReportError(string msg)
        {
            _ = Logger.Log(FOLDER_PATH, msg);
        }
    }
}