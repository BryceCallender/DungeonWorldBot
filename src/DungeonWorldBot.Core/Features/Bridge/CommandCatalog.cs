using System.Text.Json;
using System.Text.Json.Serialization;
using DungeonWorldBot.Features.Classes;

namespace DungeonWorldBot.Features.Bridge;

/// <summary>
/// What kind of value a command's option takes, for the dashboard to pick the right control for it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CatalogOptionType>))]
public enum CatalogOptionType
{
    Text,
    Integer,
    Number,
    Boolean,

    /// <summary>
    /// A Discord user, given by their ID: in practice, whose character.
    /// </summary>
    User,

    /// <summary>
    /// One of a fixed list, in <see cref="CatalogOption.Choices"/>.
    /// </summary>
    Choice,
}

/// <param name="Name">What the player sees.</param>
/// <param name="Value">What's sent to the command.</param>
public record CatalogChoice(string Name, string Value);

/// <param name="Name">The option's name as in Discord, e.g. "merge-with", which is also what the command is given it by.</param>
/// <param name="Default">What it is when left out, as text, if it has a default worth showing.</param>
/// <param name="Suggests">Whether the bot suggests values for it (Discord's autocomplete), like the forms a character knows.</param>
public record CatalogOption(
    string Name,
    string Description,
    CatalogOptionType Type,
    bool Required,
    string? Default,
    IReadOnlyList<CatalogChoice> Choices,
    bool Suggests,
    double? Min = null,
    double? Max = null);

/// <param name="Path">Its path as typed in Discord without the slash, e.g. "shapeshift into".</param>
/// <param name="Group">The top of its path, e.g. "shapeshift", which the dashboard groups commands by.</param>
/// <param name="GroupDescription">What the group is for, as Discord shows it.</param>
/// <param name="GameMasterOnly">Whether only the DM can run it.</param>
/// <param name="Mechanic">What a character needs for it to be any use to them, like Shapeshifting for /shapeshift into
/// (see <see cref="ForClassMechanicAttribute"/>), or null when it's for everyone.</param>
/// <param name="Title">What it's called on the dashboard, in plain words, e.g. "Change shape" for /shapeshift into: its
/// [DisplayName]. Null from a bot that didn't say.</param>
public record CatalogCommand(
    string Path,
    string Description,
    string Group,
    string? GroupDescription,
    bool GameMasterOnly,
    IReadOnlyList<CatalogOption> Options,
    ClassMechanic? Mechanic = null,
    string? Title = null)
{
    /// <summary>
    /// Its title, or its path when it has none.
    /// </summary>
    [JsonIgnore]
    public string Name => Title ?? Path;
}

/// <summary>
/// The bot's slash commands that can be run from the dashboard (see <see cref="Data.Entities.CommandRequest"/>), written
/// beside its database as it starts, so the dashboard can offer each one without knowing about them in advance.
/// </summary>
public record CommandCatalog(DateTimeOffset WrittenAt, IReadOnlyList<CatalogCommand> Commands)
{
    /// <summary>
    /// Where the catalog of the bot using the database at <paramref name="databasePath"/> is kept, e.g.
    /// SanaSolem.commands.json beside SanaSolem.db.
    /// </summary>
    public static string PathFor(string databasePath) => Path.ChangeExtension(Path.GetFullPath(databasePath), ".commands.json");

    /// <returns>The catalog, or null when the bot hasn't written one (or it can't be read just now).</returns>
    public static async Task<CommandCatalog?> ReadAsync(string databasePath, CancellationToken ct = default)
    {
        try
        {
            await using var file = File.OpenRead(PathFor(databasePath));
            return await JsonSerializer.DeserializeAsync(file, BridgeJson.Default.CommandCatalog, ct);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Written beside it and moved over, so the dashboard never reads half a file.
    /// </summary>
    public async Task WriteAsync(string databasePath)
    {
        var path = PathFor(databasePath);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(this, BridgeJson.Default.CommandCatalog));
        File.Move(temporary, path, overwrite: true);
    }
}
