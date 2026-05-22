namespace Client.Service
{
    /// <summary>
    /// Contrato HTTP con el Server de Canelary. Una entrada por endpoint.
    /// </summary>
    public interface ICanelaryApi
    {
        Task ValidateClientAsync(Core.Request request, CancellationToken cancellationToken = default);
        Task ReportMultipleInstallationsAsync(Core.Request request, CancellationToken cancellationToken = default);
        Task ReportNotInstalledAsync(Core.Request request, CancellationToken cancellationToken = default);
        Task<string> GetClientVersionAsync(CancellationToken cancellationToken = default);
        Task<HttpResponseMessage> DownloadInstallerAsync(CancellationToken cancellationToken = default);
    }
}
