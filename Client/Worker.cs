using Client.Options;
using Client.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Client
{
    public sealed class Worker(
        ClientService client,
        IOptions<AppOptions> appOptions,
        TimeProvider timeProvider,
        ILogger<Worker> logger) : BackgroundService
    {
        private readonly AppOptions _app = appOptions.Value;
        private readonly SemaphoreSlim _etiquetasGate = new(1, 1);
        private readonly SemaphoreSlim _piQuatroGate = new(1, 1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await client.EnsurePiPathAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "EnsurePiPathAsync fallo durante el arranque");
            }

            List<Task> tasks = [
                CheckEtiquetas(stoppingToken),
                CheckPiQuatro(stoppingToken),
                CheckUpdates(stoppingToken),
            ];

            await Task.WhenAll(tasks);
        }

        private async Task CheckEtiquetas(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _etiquetasGate.WaitAsync(stoppingToken);
                    try
                    {
                        await client.SendEtiquetas(stoppingToken);
                    }
                    finally
                    {
                        _etiquetasGate.Release();
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "CheckEtiquetas fallo en una iteracion");
                }

                // Si el tiempo intervalo no fue configurado se usa 5 minutos por default.
                double interval = _app.IntervaloMins > 0 ? _app.IntervaloMins : 5.0;
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(interval), timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) { return; }
            }
        }

        private async Task CheckPiQuatro(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeUntilNext(_app.PiquatroTime), timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) { return; }

                try
                {
                    await _piQuatroGate.WaitAsync(stoppingToken);
                    try
                    {
                        await client.CheckPiQuatroAsync(stoppingToken);
                    }
                    finally
                    {
                        _piQuatroGate.Release();
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "CheckPiQuatro fallo en una iteracion");
                }
            }
        }

        private async Task CheckUpdates(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeUntilNext(_app.UpdateTime), timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) { return; }

                try
                {
                    // Orden fijo: etiquetas -> piQuatro para evitar deadlock con los otros loops.
                    await _etiquetasGate.WaitAsync(stoppingToken);
                    try
                    {
                        await _piQuatroGate.WaitAsync(stoppingToken);
                        try
                        {
                            await client.GetUpdate(stoppingToken);
                        }
                        finally
                        {
                            _piQuatroGate.Release();
                        }
                    }
                    finally
                    {
                        _etiquetasGate.Release();
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "CheckUpdates fallo en una iteracion");
                }
            }
        }

        private TimeSpan TimeUntilNext(int hourOfDay) => TimeUntilNext(timeProvider, hourOfDay);

        /// <summary>
        /// Calcula cuanto falta hasta la proxima ocurrencia de <paramref name="hourOfDay"/> hs local.
        /// Si el calculo da cero o negativo (por ejemplo en una llamada justo en el limite), devuelve
        /// 1 minuto para evitar un loop ocioso.
        /// </summary>
        internal static TimeSpan TimeUntilNext(TimeProvider timeProvider, int hourOfDay)
        {
            DateTimeOffset now = timeProvider.GetLocalNow();
            DateTimeOffset next = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset)
                .AddDays(1)
                .AddHours(hourOfDay);
            TimeSpan remaining = next - now;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMinutes(1);
        }
    }
}
