using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Client.Logging
{
    /// <summary>
    /// Logger basado en archivos: escribe en <c>{path}/yyyy_MM_dd.log</c> a traves de un
    /// <see cref="Channel{T}"/> drenado por una sola tarea, para no bloquear los loops del worker.
    /// </summary>
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _folder;
        private readonly Channel<string> _queue;
        private readonly Task _writer;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

        public FileLoggerProvider(string folder, int capacity = 1024)
        {
            _folder = folder;
            Directory.CreateDirectory(folder);
            _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
            _writer = Task.Run(WriteLoopAsync);
        }

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

        internal bool TryEnqueue(string line) => _queue.Writer.TryWrite(line);

        private async Task WriteLoopAsync()
        {
            try
            {
                await foreach (string line in _queue.Reader.ReadAllAsync(_cts.Token))
                {
                    try
                    {
                        string file = Path.Combine(_folder, DateTime.Now.ToString("yyyy_MM_dd") + ".log");
                        await File.AppendAllTextAsync(file, line, Encoding.UTF8, _cts.Token);
                    }
                    catch
                    {
                        // No hay nada que podamos hacer si el logger falla. Continuamos.
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            _queue.Writer.TryComplete();
            try { _writer.Wait(TimeSpan.FromSeconds(2)); } catch { }
            _cts.Cancel();
            _cts.Dispose();
        }

        private sealed class FileLogger(string category, FileLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;

                string message = formatter(state, exception);
                StringBuilder sb = new();
                sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ")
                  .Append(logLevel).Append(' ')
                  .Append(category).Append(": ")
                  .AppendLine(message);
                if (exception is not null)
                {
                    sb.AppendLine(exception.ToString());
                }
                provider.TryEnqueue(sb.ToString());
            }
        }
    }
}
