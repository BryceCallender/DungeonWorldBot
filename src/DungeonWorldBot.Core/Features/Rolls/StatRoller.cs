using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Rolls;

/// <param name="RolledWith">What went into it, for the "Rolled with" field, e.g. "INT +2, Weak -1, Bonus +1".</param>
public record StatRoll(MoveRoll Roll, string RolledWith);

/// <summary>
/// Rolls 2d6 + a stat for moves rolled outside /move, like a subclass's or a shapeshifter's, with the stat's debility
/// and any subclass bonuses, as /move would.
/// </summary>
[Scoped]
public class StatRoller(IDiceRoller diceRoller, IDamageService damageService)
{
    /// <param name="bonus">Any +forward, ongoing bonus or penalty the player adds.</param>
    public async Task<StatRoll> RollAsync(Character character, StatType stat, int bonus)
    {
        var modifier = character.Stats.FirstOrDefault(s => s.StatType == stat)?.Modifier ?? 0;
        var debility = DebilityRules.RollPenalty(character, stat);
        var bonuses = await damageService.BonusesAsync(character, RollKind.Stat, stat);

        var parts = new List<string> { $"{stat.ToString().ToUpper()} {Numbers.Signed(modifier)}" };
        if (debility is { } penalty)
            parts.Add($"{penalty.Debility} {Numbers.Signed(penalty.Penalty)}");
        parts.AddRange(bonuses.Select(b => $"{b.Source} {Numbers.Signed(b.Amount)}"));
        if (bonus != 0)
            parts.Add($"Bonus {Numbers.Signed(bonus)}");

        var roll = MoveRoller.Roll(diceRoller, modifier + (debility?.Penalty ?? 0) + bonuses.Sum(b => b.Amount) + bonus);
        return new StatRoll(roll, string.Join(", ", parts));
    }
}
