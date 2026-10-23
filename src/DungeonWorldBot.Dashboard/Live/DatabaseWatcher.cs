using Microsoft.Data.Sqlite;

namespace DungeonWorldBot.Dashboard.Live;

/// <summary>
/// Notices when the bot writes to its database, so open pages can refresh themselves.
/// </summary>
/// <remarks>
/// Asks SQLite once a second whether another connection has committed anything (<c>PRAGMA data_version</c>). That
/// catches every committed write in any journal mode, and only once it's committed, so a refresh never lands halfway
/// through one. The file is checked too, in case it's swapped for another copy, which needs a fresh connection.
/// </remarks>
public class DatabaseWatcher : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly string _path;
    private readonly ILogger<DatabaseWatcher> _logger;
    private TaskCompletionSource _next = NewSignal();

    public DatabaseWatcher(string databasePath, ILogger<DatabaseWatcher> logger)
    {
        _path = databasePath;
        _logger = logger;
    }

    /// <summary>
    /// Completes the next time the database changes.
    /// </summary>
    public Task NextChange => Volatile.Read(ref _next).Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SqliteConnection? connection = null;
        long? lastVersion = null;
        var lastFile = FileIdentity();
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var changed = false;

                // A different file (restored, copied over) has its own version count, so start again on it
                var file = FileIdentity();
                if (file != lastFile)
                {
                    lastFile = file;
                    connection?.Dispose();
                    connection = null;
                    lastVersion = null;
                    changed = true;
                }

                try
                {
                    connection ??= Open();
                    var version = DataVersion(connection);
                    if (lastVersion is not null && version != lastVersion)
                        changed = true;
                    lastVersion = version;
                }
                catch (SqliteException e)
                {
                    // Missing or locked for a moment: try again next tick
                    _logger.LogDebug(e, "Couldn't check the database for changes");
                    connection?.Dispose();
                    connection = null;
                    lastVersion = null;
                }

                if (changed)
                    Interlocked.Exchange(ref _next, NewSignal()).TrySetResult();
            }
        }
        finally
        {
            connection?.Dispose();
        }
    }

    private SqliteConnection Open()
    {
        // Unpooled, so dropping it lets go of a file that's been replaced
        var connection = new SqliteConnection($"Data Source={_path};Mode=ReadOnly;Pooling=False");
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
    private string FileIdentity() =>
        new FileInfo(_path) is { Exists: true } file ? file.CreationTimeUtc.Ticks.ToString() : "-";

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
