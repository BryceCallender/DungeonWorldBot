using DungeonWorldBot.Dashboard.Models.Live;
using Microsoft.Data.Sqlite;

namespace DungeonWorldBot.Dashboard.Services.Live;

/// <summary>
/// Notices when the bot writes to its database, or anyone changes a canvas, so open pages can refresh themselves.
/// </summary>
/// <remarks>
/// Asks SQLite once a second whether another connection has committed anything (<c>PRAGMA data_version</c>). That
/// catches every committed write in any journal mode, and only once it's committed, so a refresh never lands halfway
/// through one. Each file is checked too, in case it's swapped for another copy, which needs a fresh connection.
/// </remarks>
public class DatabaseWatcher(IEnumerable<WatchedDatabase> databases, ILogger<DatabaseWatcher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly IReadOnlyList<WatchedDatabase> _databases = databases.ToList();
    private TaskCompletionSource<IReadOnlyList<string>> _next = NewSignal();

    /// <summary>
    /// Completes the next time a database changes, with the names of those that did (see <see cref="WatchedDatabase"/>),
    /// so a page that shows nothing from one needn't refresh for it.
    /// </summary>
    public Task<IReadOnlyList<string>> NextChange => Volatile.Read(ref _next).Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var watched = _databases.Select(d => new WatchedFile(d.Name, d.Path) { LastFile = FileIdentity(d.Path) }).ToList();
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                List<string> changed = [];
                foreach (var file in watched)
                {
                    if (Check(file))
                        changed.Add(file.Name);
                }

                if (changed.Count > 0)
                    Interlocked.Exchange(ref _next, NewSignal()).TrySetResult(changed);
            }
        }
        finally
        {
            foreach (var file in watched)
                file.Connection?.Dispose();
        }
    }

    // Whether this file has changed since the last tick
    private bool Check(WatchedFile watched)
    {
        var changed = false;

        // A different file (restored, copied over) has its own version count, so start again on it
        var file = FileIdentity(watched.Path);
        if (file != watched.LastFile)
        {
            watched.LastFile = file;
            watched.Connection?.Dispose();
            watched.Connection = null;
            watched.LastVersion = null;
            changed = true;
        }

        try
        {
            watched.Connection ??= Open(watched.Path);
            var version = DataVersion(watched.Connection);
            if (watched.LastVersion is not null && version != watched.LastVersion)
                changed = true;
            watched.LastVersion = version;
        }
        catch (SqliteException e)
        {
            // Missing or locked for a moment: try again next tick
            logger.LogDebug(e, "Couldn't check {Path} for changes", watched.Path);
            watched.Connection?.Dispose();
            watched.Connection = null;
            watched.LastVersion = null;
        }

        return changed;
    }

    private static SqliteConnection Open(string path)
    {
        // Unpooled, so dropping it lets go of a file that's been replaced
        var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        return connection;
    }

    private static long DataVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA data_version";
        return (long)command.ExecuteScalar()!;
    }

    // Creation time changes when the file is replaced; edits in place don't touch it
    private static string FileIdentity(string path) =>
        new FileInfo(path) is { Exists: true } file ? file.CreationTimeUtc.Ticks.ToString() : "-";

    private static TaskCompletionSource<IReadOnlyList<string>> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    // One database being watched, and what it looked like last tick
    private record WatchedFile(string Name, string Path)
    {
        public SqliteConnection? Connection { get; set; }
        public long? LastVersion { get; set; }
        public string LastFile { get; set; } = "-";
    }
}
