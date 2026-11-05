using DungeonWorldBot.Data.Entities;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// Everything done in a fight: the actions logged in it, and the 2d6 rolls in game chat that belong to it.
/// </summary>
/// <param name="Actions">Moves, NPC rolls and enemy attacks, oldest first.</param>
/// <param name="Rolls">This fight's rolls, oldest first. Some are also logged actions; see <see cref="Logged"/>.</param>
public record FightLog(IReadOnlyList<EncounterAction> Actions, IReadOnlyList<RollRecord> Rolls)
{
    /// <summary>
    /// The logged actions that were rolls too, by the bot's roll message. The log says more about them.
    /// </summary>
    public IReadOnlyDictionary<Snowflake, EncounterAction> Logged { get; } =
        Actions.Where(a => a.MessageID is not null).ToDictionary(a => a.MessageID!.Value);

    /// <summary>
    /// The roll messages among <see cref="Rolls"/>, for leaving out logged actions already counted as a roll.
    /// </summary>
    public IReadOnlySet<Snowflake> Rolled { get; } = Rolls.Select(r => r.MessageID).ToHashSet();
}
