using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Subclasses;
using Remora.Discord.API.Abstractions.Objects;

namespace DungeonWorldBot.Features.Rolls;

/// <summary>
/// What a roll was for, read off the bot's roll embed. Live rolls and rolls found in chat history are read the same
/// way, so both are counted alike.
/// </summary>
/// <param name="Move">The move rolled, when the title names one, e.g. "🎲 Zephyr: Volley" or ".dex volley".</param>
/// <param name="Stat">The stat the roll went with, from the start of "Rolled with", e.g. "DEX +2, Bonus +1".</param>
public record RollLabel(string? Move, StatType? Stat)
{
    public const string RolledWith = "Rolled with";

    // Built on first use, since the class sheets and subclasses are static too
    private static readonly Lazy<Dictionary<string, string>> KnownMoves = new(() =>
        new[]
            {
                BasicMoves.HackAndSlash, BasicMoves.Volley, BasicMoves.DefyDanger, BasicMoves.Defend, BasicMoves.SpoutLore,
                BasicMoves.DiscernRealities, BasicMoves.Parley, BasicMoves.AidOrInterfere, BasicMoves.LastBreath,
            }
            .Select(m => m.Name)
            .Concat(ClassSheetLibrary.All.SelectMany(s => s.AllMoves).Select(m => m.Name))
            .Concat(SubclassLibrary.All.SelectMany(s => s.Moves).Select(m => m.Move.Name))
            .DistinctBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(n => n, n => n, StringComparer.OrdinalIgnoreCase));

    public static RollLabel Of(IEmbed embed)
    {
        var fields = embed.Fields.OrDefault(null) ?? [];
        var rolledWith = fields.FirstOrDefault(f => f.Name == RolledWith)?.Value;
        return new RollLabel(MoveIn(embed.Title.OrDefault(null)), StatIn(rolledWith));
    }

    /// <returns>The move's name as the bot spells it, or null if the title's label (after the ": ") isn't a move.</returns>
    public static string? MoveIn(string? title)
    {
        var colon = title?.IndexOf(": ", StringComparison.Ordinal) ?? -1;
        if (colon < 0)
            return null;

        return KnownMoves.Value.GetValueOrDefault(title![(colon + 2)..].Trim());
    }

    public static StatType? StatIn(string? rolledWith)
    {
        var first = rolledWith?.Split(',')[0].Trim().Split(' ')[0];
        return first is { Length: 3 } && first.All(char.IsLetter) && Enum.TryParse<StatType>(first, ignoreCase: true, out var stat) ? stat : null;
    }
}
