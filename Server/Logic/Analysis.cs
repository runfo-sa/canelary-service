using Core;
using Microsoft.IdentityModel.Tokens;
using Server.Models;
using System.Collections.Frozen;

namespace Server.Logic
{
    public static class Analysis
    {
        /// <summary>
        /// Base path para los logs por-cliente. Default: <c>CommonApplicationData/Canelary Server</c>.
        /// El env var <c>CANELARY_SERVER_LOG_BASE</c> permite overrideo (lo usan los tests para
        /// no escribir en <c>/usr/share</c> en CI Linux).
        /// </summary>
        internal static string GetLogBasePath() => Path.Combine(
            Environment.GetEnvironmentVariable("CANELARY_SERVER_LOG_BASE")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Canelary Server");

        /// <summary>
        /// Función que compara la lista de <see cref="Etiqueta"/> del cliente con las del servidor.
        /// <br/>
        /// Analiza en busca de archivos faltantes, sobrantes o distintos.
        /// </summary>
        /// <returns>
        /// <see cref="Status"/> - Estado del cliente, junto con la lista de etiquetas
        /// que difieren (faltantes o sobrantes) para persistir en la DB.
        /// </returns>
        public static (Status Status, List<EtiquetaCliente> Diff) CheckClient(Request client, FrozenSet<Etiqueta> serverEtiquetas)
        {
            FrozenSet<Etiqueta> clientEtiquetas = client.Etiquetas!.ToFrozenSet();

            // Obtenemos el conjunto distinto del servidor
            IEnumerable<Etiqueta> desactualizadas = serverEtiquetas.Except(clientEtiquetas);
            IEnumerable<Etiqueta>? sobrantes = null;

            // Chequeamos si tiene archivos sobrantes
            if (clientEtiquetas.Count > serverEtiquetas.Count)
            {
                sobrantes = clientEtiquetas.Except(serverEtiquetas, new EtiquetaCompareName());
            }

            Status status = (sobrantes is not null) ?
                ((desactualizadas.IsNullOrEmpty()) ? Status.Sobrantes : Status.DesactualizadaSobrantes)
                : ((desactualizadas.IsNullOrEmpty()) ? Status.Okay : Status.Desactualizada);

            var diff = new List<EtiquetaCliente>();

            if (status != Status.Okay)
            {
                var path = Path.Combine(GetLogBasePath(), client.Name);

                string list = (sobrantes is not null && status == Status.Sobrantes) ?
                    $"Sobrantes:{Environment.NewLine}" + string.Join(Environment.NewLine, sobrantes) : (sobrantes is not null) ?

                    $"Desactualizadas:{Environment.NewLine}" + string.Join(Environment.NewLine, desactualizadas) +
                    $"{Environment.NewLine}Sobrantes:{Environment.NewLine}" + string.Join(Environment.NewLine, sobrantes)

                    : $"Desactualizadas:{Environment.NewLine}" + string.Join(Environment.NewLine, desactualizadas);

                Logger.Log(path, list);

                if (status is Status.Desactualizada or Status.DesactualizadaSobrantes)
                {
                    foreach (var e in desactualizadas)
                    {
                        diff.Add(new EtiquetaCliente(e.Name, TipoDiff.Desactualizada));
                    }
                }

                if (sobrantes is not null)
                {
                    foreach (var e in sobrantes)
                    {
                        diff.Add(new EtiquetaCliente(e.Name, TipoDiff.Sobrante));
                    }
                }
            }

            return (status, diff);
        }
    }
}