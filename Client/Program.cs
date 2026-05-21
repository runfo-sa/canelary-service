using System.Diagnostics;
using Client;
using Client.Logging;
using Client.Options;
using Client.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal class Program
{
    private const string ServiceName = "Canelary - Controlador de Etiquetas";

    private static void Main(string[] args)
    {
        try
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddWindowsService(options => options.ServiceName = ServiceName);

            // Logging registrado ANTES del resto del DI, para que cualquier excepcion al construir
            // singletons (ConfigService, ClientService, etc.) caiga en el archivo y en el EventLog
            // que AddWindowsService engancha automaticamente.
            var logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Canelary Service", "Logs");
            builder.Logging.AddProvider(new FileLoggerProvider(logFolder));

            // ConfigService sigue siendo la fuente de verdad para el archivo JSON con comentarios.
            builder.Services.AddSingleton<ConfigService>();

            // Las tres secciones se exponen via options pattern para los consumidores DI-friendly.
            // Snapshot al arranque: los cambios en el archivo se aplicaran al reiniciar el servicio.
            builder.Services.AddSingleton<IOptions<ServerOptions>>(sp =>
            {
                var data = sp.GetRequiredService<ConfigService>().Data;
                return Options.Create(new ServerOptions
                {
                    Ip = data.Server!.Ip,
                    Port = data.Server!.Port,
                    Scheme = string.IsNullOrWhiteSpace(data.Server!.Scheme) ? "http" : data.Server!.Scheme!,
                });
            });
            builder.Services.AddSingleton<IOptions<AuthOptions>>(sp =>
            {
                var data = sp.GetRequiredService<ConfigService>().Data;
                return Options.Create(new AuthOptions
                {
                    ClavePublica = data.Auth!.ClavePublica,
                    ClavePrivada = data.Auth!.ClavePrivada,
                    ClaveDescarga = data.Auth!.ClaveDescarga,
                });
            });
            builder.Services.AddSingleton<IOptions<AppOptions>>(sp =>
            {
                var data = sp.GetRequiredService<ConfigService>().Data;
                return Options.Create(new AppOptions
                {
                    Unidad = data.App!.Unidad,
                    UpdateTime = data.App!.UpdateTime,
                    PiquatroTime = data.App!.PiquatroTime,
                    IntervaloMins = data.App!.IntervaloMins,
                    PiPath = data.App!.PiPath,
                });
            });

            // Typed HttpClient con resilience handler y el AuthHeaderHandler delegating handler.
            builder.Services.AddTransient<AuthHeaderHandler>();
            builder.Services.AddHttpClient<ICanelaryApi, CanelaryApiClient>((sp, http) =>
            {
                var server = sp.GetRequiredService<IOptions<ServerOptions>>().Value;
                // Scheme (http/https) configurable; default http para mantener compat con servidores legacy.
                http.BaseAddress = new Uri($"{server.Scheme}://{server.Ip}:{server.Port}/");
            })
            .AddHttpMessageHandler<AuthHeaderHandler>()
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 2;
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            });

            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<ClientService>();
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
        catch (Exception ex)
        {
            // Cualquier excepcion durante DI/host build se reporta a multiples canales para
            // que jamas caiga en silencio (sintoma original del Error 1067 sin logs).
            try { Reporter.ReportError(ex.ToString()); } catch { }
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    EventLog.WriteEntry(ServiceName, ex.ToString(), EventLogEntryType.Error);
                }
            }
            catch { }
            Environment.Exit(1);
        }
    }
}
