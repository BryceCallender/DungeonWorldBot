using System.Text.RegularExpressions;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Rolls;

namespace DungeonWorldBot.Features.Npcs;

/// <summary>
/// Something an NPC can roll with.
/// </summary>
/// <param name="Name">What it's called, e.g. "STR", "Loyalty", "Protector" or "Damage".</param>
/// <param name="Dice">What gets rolled, modifier included, e.g. "2d6+2" or "d6".</param>
/// <param name="Modifier">What's added to 2d6, or null for a damage roll.</param>
public record NpcRollOption(string Name, string Dice, int? Modifier);

public static partial class NpcRolls
{
    public const string Loyalty = "Loyalty";
    public const string Damage = "Damage";

    // "Protector +2", "Tracker+1", "Burglar -1"
    [GeneratedRegex(@"^\s*(?<name>.*?)\s*(?<mod>[+-]\s*\d+)\s*$")]
    private static partial Regex SkillPattern();

    /// <summary>
    /// The skills written in an NPC's skills, e.g. "Protector +2, Tracker +1". Skills without a number are left out,
    /// since there's nothing to roll with.
    /// </summary>
    public static IReadOnlyList<(string Name, int Modifier)> Skills(Npc npc) =>
        (npc.Skills ?? string.Empty)
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => SkillPattern().Match(s))
            .Where(m => m.Success && m.Groups["name"].Length > 0)
            .Select(m => (m.Groups["name"].Value, int.Parse(m.Groups["mod"].Value.Replace(" ", ""))))
            .ToList();

    /// <summary>
    /// Everything the NPC can roll with: their stats, loyalty, skills, then their damage if they have damage dice.
    /// </summary>
    public static IReadOnlyList<NpcRollOption> Options(Npc npc)
    {
        var options = Enum.GetValues<StatType>()
            .Select(s => Move(s.ToString().ToUpper(), npc.Modifier(s)))
            .Append(Move(Loyalty, npc.Loyalty))
            .Concat(Skills(npc).Select(s => Move(s.Name, s.Modifier)))
            .ToList();

        if (npc.Damage is not null)
            options.Add(new NpcRollOption(Damage, npc.Damage, null));

        return options;
    }

    /// <summary>
    /// The option with the given name, whatever its case.
    /// </summary>
    public static NpcRollOption? Find(Npc npc, string name) =>
        Options(npc).FirstOrDefault(o => o.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    private static NpcRollOption Move(string name, int modifier) => new(name, DiceHelper.AddModifier("2d6", modifier), modifier);
}
