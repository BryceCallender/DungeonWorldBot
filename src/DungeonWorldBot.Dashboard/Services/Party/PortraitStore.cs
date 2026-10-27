using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Dashboard.Services.Party;

/// <summary>
/// Characters' pictures, kept on their rows in the bot's database (see <see cref="CharacterPortrait"/>).
/// </summary>
public class PortraitStore
{
    private readonly DbContextOptions<DungeonWorldContext> _options;

    /// <param name="options">Options for opening the bot's database read-write.</param>
    public PortraitStore(DbContextOptions<DungeonWorldContext> options)
    {
        _options = options;
    }

    /// <summary>
    /// Where a character's picture is served, with when it was set so a new one gets a new address.
    /// </summary>
    public static string Url(ulong characterID, DateTime updatedAt) => $"api/characters/{characterID}/portrait?v={updatedAt.Ticks}";

    public async Task<CharacterPortrait?> FindAsync(ulong characterID)
    {
        await using var db = new DungeonWorldContext(_options);
        var id = new Snowflake(characterID);
        return await db.Set<CharacterPortrait>().AsNoTracking().FirstOrDefaultAsync(p => p.CharacterID == id);
    }

    /// <summary>
    /// Every character with a picture, and its address, without loading the pictures.
    /// </summary>
    public async Task<Dictionary<ulong, string>> UrlsAsync()
    {
        await using var db = new DungeonWorldContext(_options);
        return (await db.Set<CharacterPortrait>().Select(p => new { p.CharacterID, p.UpdatedAt }).ToListAsync())
            .ToDictionary(p => p.CharacterID.Value, p => Url(p.CharacterID.Value, p.UpdatedAt));
    }

    public Task SaveAsync(ulong characterID, string contentType, byte[] data) =>
        ChangeAsync(characterID, c => c.Portrait = new CharacterPortrait
        {
            CharacterID = c.ID,
            ContentType = contentType,
            Data = data,
            UpdatedAt = DateTime.UtcNow,
        });

    public Task RemoveAsync(ulong characterID) =>
        ChangeAsync(characterID, c => c.Portrait = null);

    /// <summary>
    /// Moves pictures from where they used to be kept, the canvas database's CharacterPortraits table, onto the
    /// characters' rows, before the canvas migration that drops that table. A character who already has a picture on
    /// their row keeps it.
    /// </summary>
    /// <returns>How many were moved.</returns>
    /// <exception cref="InvalidOperationException">The bot's database doesn't have the picture columns yet, so the
    /// table mustn't be dropped.</exception>
    public static int MoveFromCanvases(string databasePath, string canvasDatabasePath)
    {
        if (!File.Exists(databasePath) || !File.Exists(canvasDatabasePath))
            return 0;

        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadWrite");
        connection.Open();
        var attach = Command("ATTACH DATABASE $canvases AS canvases");
        attach.Parameters.AddWithValue("$canvases", canvasDatabasePath);
        attach.ExecuteNonQuery();
        if (Convert.ToInt64(Command("SELECT COUNT(*) FROM canvases.sqlite_master WHERE type = 'table' AND name = 'CharacterPortraits'").ExecuteScalar()) == 0)
            return 0;

        try
        {
            return Command("""
                UPDATE Characters
                SET PortraitContentType = p.ContentType, PortraitData = p.Data, PortraitUpdatedAt = p.UpdatedAt
                FROM canvases.CharacterPortraits AS p
                WHERE p.CharacterID = Characters.ID AND Characters.PortraitData IS NULL
                """).ExecuteNonQuery();
        }
        catch (SqliteException e) when (e.Message.Contains("no such column"))
        {
            throw new InvalidOperationException(
                $"{databasePath} has no picture columns yet, so characters' pictures can't be moved there. Bring it up to " +
                "date with the bot's migrations (CharacterPortraits) before starting the dashboard.", e);
        }

        SqliteCommand Command(string sql)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            return command;
        }
    }

    // Through the character, since the picture shares their row: setting or clearing it updates that row's columns
    private async Task ChangeAsync(ulong characterID, Action<Character> change)
    {
        await using var db = new DungeonWorldContext(_options);
        var id = new Snowflake(characterID);
        if (await db.Characters.Include(c => c.Portrait).FirstOrDefaultAsync(c => c.ID == id) is not { } character)
            return;

        change(character);
        await db.SaveChangesAsync();
    }
}
