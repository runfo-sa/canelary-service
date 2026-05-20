using System.Net.Http.Headers;
using System.Net.Http.Json;
using Client.Serialization;
using Core;

namespace Client.Service
{
    public sealed class CanelaryApiClient(HttpClient http) : ICanelaryApi
    {
        public async Task ValidateClientAsync(Request request, CancellationToken cancellationToken = default)
        {
            using var response = await http.PostAsJsonAsync("/validate-client", request, CanelaryJsonContext.Default.Request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public async Task ReportMultipleInstallationsAsync(Request request, CancellationToken cancellationToken = default)
        {
            using var response = await http.PostAsJsonAsync("/multiple-installations", request, CanelaryJsonContext.Default.Request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public async Task ReportNotInstalledAsync(Request request, CancellationToken cancellationToken = default)
        {
            using var response = await http.PostAsJsonAsync("/not-installed", request, CanelaryJsonContext.Default.Request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public async Task<string> GetClientVersionAsync(CancellationToken cancellationToken = default)
        {
            using var response = await http.GetAsync("/client-version", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        public async Task<HttpResponseMessage> DownloadInstallerAsync(string descargaKey, CancellationToken cancellationToken = default)
        {
            // Manda la clave en Authorization: Bearer en vez de query-string para evitar
            // que aparezca en logs de IIS/Kestrel. El Server tambien acepta ?key= como fallback
            // transitorio para no romper clientes ya desplegados.
            using var request = new HttpRequestMessage(HttpMethod.Get, "/installer");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", descargaKey);
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
    }
}
