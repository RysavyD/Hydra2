using Microsoft.Extensions.Logging;

namespace Hydra2.DownLoad;

/// <summary>
/// Synchronní konzolový logger. Standardní AddConsole() zapisuje z fronty na pozadí,
/// takže se logy míchají s výstupem Console.WriteLine – tady jdou ve správném pořadí.
/// </summary>
public sealed class SyncConsoleLoggerProvider : ILoggerProvider
{
    private static readonly object Lock = new();

    public ILogger CreateLogger(string categoryName) => new SyncConsoleLogger(categoryName);

    public void Dispose()
    {
    }

    private sealed class SyncConsoleLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var (label, color) = logLevel switch
            {
                LogLevel.Trace => ("trce", ConsoleColor.DarkGray),
                LogLevel.Debug => ("dbug", ConsoleColor.Gray),
                LogLevel.Information => ("info", ConsoleColor.Green),
                LogLevel.Warning => ("warn", ConsoleColor.Yellow),
                LogLevel.Error => ("fail", ConsoleColor.Red),
                _ => ("crit", ConsoleColor.Magenta),
            };

            lock (Lock)
            {
                Console.Write($"{DateTime.Now:HH:mm:ss.fff} ");
                Console.ForegroundColor = color;
                Console.Write(label);
                Console.ResetColor();
                Console.WriteLine($" [{category}] {formatter(state, exception)}");
                if (exception is not null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(exception);
                    Console.ResetColor();
                }
            }
        }
    }
}
