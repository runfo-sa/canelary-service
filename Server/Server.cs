using Core;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Server.Logic;
using Server.Models;
using Server.Serialization;
using System.Net;
using System.Threading.RateLimiting;

namespace Server
{
    public static class Server
    {
        private static readonly string ClientFolder = "client-repo";
        private static readonly string ClientFile = "Client.exe";
        private static readonly string InstallerFile = "installer.ps1";

        /// <summary>
        /// Resuelve la clave de descarga preferentemente desde el header <c>Authorization: Bearer ...</c>,
        /// y como fallback transitorio desde el query string <c>?key=...</c> para no romper clientes ya
        /// desplegados que usan el esquema anterior.
        /// </summary>
        private static string? ResolveDownloadKey(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue("Authorization", out var auth))
            {
                string value = auth.ToString();
                const string prefix = "Bearer ";
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return value.Substring(prefix.Length);
                }
            }
            return context.Request.Query["key"];
        }

        public static void Run(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<ClientStatusDb>();

            // Options pattern para la seccion Auth.
            // El builder ya incluye AddEnvironmentVariables() por default, asi que en produccion
            // se puede setear `Auth__ClavePublica`, `Auth__ClavePrivada`, `Auth__ClaveDescarga`
            // como env vars y sacar el bloque de appsettings.json sin tocar codigo.
            builder.Services.AddOptions<AuthConfig>()
                .BindConfiguration("Auth")
                .ValidateOnStart();

            // JSON source generators para los DTOs compartidos via Core.
            builder.Services.ConfigureHttpJsonOptions(o =>
                o.SerializerOptions.TypeInfoResolverChain.Insert(0, CanelaryJsonContext.Default));

            // Problem details (RFC 9457) + exception handler para devolver fallos del server
            // como JSON estructurado en vez de paginas HTML por default.
            builder.Services.AddProblemDetails();

            // Healthcheck que verifica que la conexion al SQL Server (memory-optimized) este viva.
            builder.Services.AddHealthChecks()
                .AddDbContextCheck<ClientStatusDb>(name: "database");

            // Swager Docs
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
                {
                    Title = "Canelary - Controlador de Etiquetas",
                    Description = "Web API encargada de reportar el estado de etiquetas en cada puesto",
                    Version = "v1"
                });
            });

            // CORS abierto para el dashboard web servido desde otro origen en la LAN.
            builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
                p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

            // Watchers singleton
            builder.Services.AddSingleton<WatcherPiQuatro>();
            builder.Services.AddSingleton(new WatcherClient(ClientFolder, ClientFile));

            // Limita la cantidad de pedidos a 100 cada 5 minutos
            builder.Services.AddRateLimiter(_ => _
                .AddFixedWindowLimiter(policyName: "fixed", options =>
                {
                    options.PermitLimit = 100;
                    options.Window = TimeSpan.FromMinutes(10);
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 50;
                })
            );

            var app = builder.Build();

            // ExceptionHandler debe ir antes que cualquier middleware que pueda fallar.
            // Devuelve ProblemDetails (RFC 9457) al cliente sin filtrar excepciones internas.
            app.UseExceptionHandler();

            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseRateLimiter();

            // Healthcheck endpoint exento de auth y rate-limiting; usable por load balancers / oncall.
            app.MapHealthChecks("/healthz").DisableRateLimiting();

            var authConfig = app.Services.GetRequiredService<IOptions<AuthConfig>>().Value;
            var piQuatroWatch = app.Services.GetService<WatcherPiQuatro>();
            var clientWatch = app.Services.GetService<WatcherClient>();

            app.Use(async (context, next) =>
            {
                // Middleware que verifica si es una conexión valida y la termina en caso de no serlo.
                // Solamente para estos endpoints.
                if (context.Request.Path.StartsWithSegments("/validate-client") ||
                    context.Request.Path.StartsWithSegments("/multiple-installations"))
                {
                    string? key = context.Request.Headers["request-key"];
                    string? hash = context.Request.Headers["request-hash"];

                    if (!AuthValidator.IsRequestAuthorized(key, hash, authConfig.ClavePrivada))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        await context.Response.WriteAsync("Unauthorized");
                        return;
                    }
                }

                await next();
            });

            app.UseRouting();

            app.UseCors();

            // API Endpoints:
            app.MapPost("/validate-client", async (ClientStatusDb db, Request client, HttpContext context) =>
            {
                (Status status, List<EtiquetaCliente> diff) = Analysis.CheckClient(client, piQuatroWatch!.ServerEtiquetas);
                var name = (context.Connection.RemoteIpAddress is not null) ? context.Connection.RemoteIpAddress.ToString() : client.Name;

                ClientStatus? clientStatus = db.EstadoCliente
                    .Include(c => c.Etiquetas)
                    .FirstOrDefault(c => c.Cliente == name);

                if (clientStatus is null)
                {
                    clientStatus = new ClientStatus(name, status);
                    await db.EstadoCliente.AddAsync(clientStatus);
                }
                else
                {
                    clientStatus.Estado = status;
                    clientStatus.UltimaConexion = DateTime.Now;
                    db.EtiquetasCliente.RemoveRange(clientStatus.Etiquetas);
                    clientStatus.Etiquetas.Clear();
                }

                foreach (var d in diff)
                {
                    clientStatus.Etiquetas.Add(d);
                }

                await db.SaveChangesAsync();

                return TypedResults.Ok(Enum.GetName(typeof(Status), status));
            });

            app.MapPost("/multiple-installations", async (ClientStatusDb db, Request client, HttpContext context) =>
            {
                var name = (context.Connection.RemoteIpAddress is not null) ? context.Connection.RemoteIpAddress.ToString() : client.Name;

                ClientStatus? clientStatus = db.EstadoCliente
                    .Include(c => c.Etiquetas)
                    .FirstOrDefault(c => c.Cliente == name);

                if (clientStatus is null)
                {
                    await db.EstadoCliente.AddAsync(new ClientStatus(name, Status.MultipleInstalaciones));
                }
                else
                {
                    clientStatus.Estado = Status.MultipleInstalaciones;
                    clientStatus.UltimaConexion = DateTime.Now;
                    db.EtiquetasCliente.RemoveRange(clientStatus.Etiquetas);
                    clientStatus.Etiquetas.Clear();
                }
                await db.SaveChangesAsync();

                var path = Path.Combine(Analysis.GetLogBasePath(), name);

                Logger.Log(path, $"Se encontraron mas de una instalación de PiQuatro:{Environment.NewLine}{client.Message}");

                return TypedResults.Ok(Enum.GetName(typeof(Status), Status.MultipleInstalaciones));
            });

            app.MapPost("/not-installed", async (ClientStatusDb db, Request client, HttpContext context) =>
            {
                var name = (context.Connection.RemoteIpAddress is not null) ? context.Connection.RemoteIpAddress.ToString() : client.Name;

                ClientStatus? clientStatus = db.EstadoCliente
                    .Include(c => c.Etiquetas)
                    .FirstOrDefault(c => c.Cliente == name);

                if (clientStatus is null)
                {
                    await db.EstadoCliente.AddAsync(new ClientStatus(name, Status.NoInstalado));
                }
                else
                {
                    clientStatus.Estado = Status.NoInstalado;
                    clientStatus.UltimaConexion = DateTime.Now;
                    db.EtiquetasCliente.RemoveRange(clientStatus.Etiquetas);
                    clientStatus.Etiquetas.Clear();
                }
                await db.SaveChangesAsync();

                var path = Path.Combine(Analysis.GetLogBasePath(), name);

                Logger.Log(path, "No se encontro una instalación de PiQuatro.");

                return TypedResults.Ok(Enum.GetName(typeof(Status), Status.NoInstalado));
            });

            app.MapGet("/get-client", async (HttpContext context) =>
            {
                string? key = ResolveDownloadKey(context);
                if (!AuthValidator.IsDownloadKeyValid(key, authConfig.ClaveDescarga))
                {
                    return Results.Unauthorized();
                }

                var bytes = await File.ReadAllBytesAsync(Path.Combine(ClientFolder, ClientFile));
                return Results.File(bytes, "application/vnd.microsoft.portable-executable", ClientFile);
            });

            app.MapGet("/installer", async (HttpContext context) =>
            {
                string? key = ResolveDownloadKey(context);
                if (!AuthValidator.IsDownloadKeyValid(key, authConfig.ClaveDescarga))
                {
                    return Results.Unauthorized();
                }

                var bytes = await File.ReadAllBytesAsync(Path.Combine(ClientFolder, InstallerFile));
                return Results.File(bytes, "text/plain", InstallerFile);
            });

            app.MapGet("/client-version", () =>
            {
                return TypedResults.Ok(clientWatch!.ClientHash);
            });

            app.MapGet("/clients", async (ClientStatusDb db) =>
            {
                var clients = await db.EstadoCliente
                    .AsNoTracking()
                    .Include(c => c.Etiquetas)
                    .OrderByDescending(c => c.UltimaConexion)
                    .ToListAsync();
                return TypedResults.Ok(clients);
            });

            app.Run();
        }
    }
}