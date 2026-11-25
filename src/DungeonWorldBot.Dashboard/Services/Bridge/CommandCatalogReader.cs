using DungeonWorldBot.Features.Bridge;

namespace DungeonWorldBot.Dashboard.Services.Bridge;

/// <summary>
/// The bot's commands as it last said (see <see cref="CommandCatalog"/>), read again only when it writes a new list,
/// as it does each time it starts.
/// </summary>
/// <param name="gamePath">The bot's SanaSolem.db, which the list is written beside.</param>
public class CommandCatalogReader(string gamePath)
{
    private CommandCatalog? _catalog;
    private DateTime _readAt;

    public async Task<CommandCatalog?> ReadAsync(CancellationToken ct = default)
    {
        var written = File.GetLastWriteTimeUtc(CommandCatalog.PathFor(gamePath));
        if (_catalog is not null && written == _readAt)
            return _catalog;

        // Kept as it was if it can't be read just now, as while the bot is writing it
        if (await CommandCatalog.ReadAsync(gamePath, ct) is { } fresh)
            (_catalog, _readAt) = (fresh, written);
        return _catalog;
    }
}
