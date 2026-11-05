using Microsoft.Extensions.Logging;

namespace DungeonWorldBot.Infrastructure.Telemetry;

/// <summary>
/// The last few errors logged, kept in memory for the dashboard's health page: the bot's go into its status file, the
/// dashboard's are shown as they are.
/// </summary>
public class RecentErrors(TimeProvider time) : ILoggerProvider
{
    public const int Kept = 25;

    private readonly LinkedList<LoggedError> _errors = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// The errors kept, newest first.
    /// </summary>
    public IReadOnlyList<LoggedError> Latest
    {
        get
        {
            lock (_lock)
                return _errors.ToList();
        }
    }

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose() => GC.SuppressFinalize(this);

    private void Add(LoggedError error)
    {
        lock (_lock)
        {
            _errors.AddFirst(error);
            if (_errors.Count > Kept)
                _errors.RemoveLast();
        }
    }

    private DateTimeOffset Now => time.GetUtcNow();

    private sealed class Logger(RecentErrors errors, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            errors.Add(new LoggedError(
                errors.Now,
                logLevel.ToString(),
                category,
                formatter(state, exception),
                exception is null ? null : $"{exception.GetType().Name}: {exception.Message}"));
        }
    }
}
