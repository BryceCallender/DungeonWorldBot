namespace DungeonWorldBot.Dashboard.Party;

/// <summary>
/// Notices when the bot writes to its database, so open pages can refresh themselves.
/// </summary>
/// <remarks>
/// Polls the database file and its write-ahead log rather than using a file watcher: two file checks a second cost
/// nothing, and it can't be set off by the dashboard's own reads.
/// </remarks>
public sealed class DatabaseWatcher : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly string[] _files;
    private TaskCompletionSource _next = NewSignal();

    public DatabaseWatcher(string databasePath)
    {
        // Only the bot writes these. Readers can touch "-shm", which would make every refresh trigger another.
        _files = [databasePath, databasePath + "-wal"];
    }

    /// <summary>
    /// Completes the next time the database changes.
    /// </summary>
    public Task NextChange => Volatile.Read(ref _next).Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var last = Fingerprint();
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = Fingerprint();
            if (now == last)
                continue;

            last = now;
            Interlocked.Exchange(ref _next, NewSignal()).TrySetResult();
        }
    }

    private string Fingerprint() =>
        string.Join('|', _files.Select(path => new FileInfo(path) is { Exists: true } file
            ? $"{file.Length}:{file.LastWriteTimeUtc.Ticks}"
            : "-"));

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
