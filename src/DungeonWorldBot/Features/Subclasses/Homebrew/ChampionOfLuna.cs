using System.Text.RegularExpressions;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Subclasses.Homebrew;

public enum LunaBoon
{
    Wrath,
    Aptitude,
    Veil,
    Fortune,
}

/// <summary>
/// A Vow declared in Luna's name.
/// </summary>
/// <param name="Stat">The stat a Boon of Aptitude raises.</param>
public sealed record LunaVow(LunaBoon Boon, StatType? Stat, string Objective)
{
    public string BoonName => $"Boon of {Boon}" + (Stat is { } stat ? $" ({stat.ToString().ToUpper()})" : string.Empty);
}

/// <summary>
/// Champion of Luna, Bearer of the Forgotten Moon: a homebrew Ranger subclass.
/// </summary>
public static partial class ChampionOfLuna
{
    public const string Name = "Champion of Luna";
    public const string BloodMoon = "Blood Moon";
    public const string LunasVow = "Luna's Vow";
    public const string Revelation = "Revelation";
    public const string LunarBarrage = "Lunar Barrage";
    public const string LifeForLife = "Life for Life";

    /// <summary>
    /// The ability holding the character's current Vow, if they have one.
    /// </summary>
    public const string ActiveVow = "Active Vow";

    /// <summary>
    /// Lunar Barrage keeps rolling while the d10 shows this or higher.
    /// </summary>
    public const int BarrageExplodesOn = 6;

    // A run of 6+ this long is about a 1 in 10^30 chance, so this only guards against a broken die
    private const int MaxBarrageRolls = 100;

    public static readonly Subclass Subclass = new(
        Name,
        "Bearer of the Forgotten Moon",
        "Homebrew",
        [ClassType.Ranger],
        "Sworn to Luna, the forgotten moon. Offer your life's blood to empower your strikes and swear Vows that share Luna's power with your party.",
        [
            new(1, new(BloodMoon,
                "When you make an attack, you may offer life to Luna to empower the strike. Sacrifice any amount of your current HP; any willing party " +
                "member may also contribute any amount of theirs. Each 1 HP sacrificed adds +1 damage. After the attack deals damage, roll +CHA. " +
                "**10+:** All HP sacrificed is recovered. **7-9:** Half of the HP sacrificed is recovered, rounded down. " +
                "**6-:** None is recovered, and the GM makes a move.\nRecovered HP returns to whoever sacrificed it. Use /subclass luna blood-moon")),
            new(1, new(LunasVow,
                "When you declare a specific objective in Luna's name, choose one Boon of Luna. You receive its full power; party members travelling " +
                "and fighting alongside you receive its lesser Echo. Only one Vow at a time. It lasts until the objective is completed, abandoned, or " +
                "becomes impossible.\n" +
                "• **Wrath:** +6 damage. *Echo:* +3 damage\n" +
                "• **Aptitude:** +2 to a chosen stat. *Echo:* +1 to it\n" +
                "• **Veil:** See and physically interact with magical constructs and effects, and try to disarm, disrupt or dispel them. " +
                "*Echo:* Allies near you see hidden magic and recognise when something is being affected by it\n" +
                "• **Fortune:** +2 to Last Breath. *Echo:* +1 to Last Breath\n" +
                "Use /subclass luna vow")),
            new(3, Rolled(Revelation, StatType.Wis,
                "When you call upon Luna to reveal knowledge that would otherwise be inaccessible, choose Scan (an enemy you can see) or Reveal (a person you can see)",
                "Ask 3 questions. Luna's answers are truthful.",
                "Ask 2 questions. Luna's answers are truthful.",
                "Ask 1 question, but the GM makes a move.",
                options:
                [
                    "Scan: What special abilities or moves does this enemy possess?",
                    "Scan: What resistances, immunities, or unusual defenses does it possess?",
                    "Scan: What weaknesses or vulnerabilities does it possess?",
                    "Scan: What weapons, magical items, or significant objects does it possess?",
                    "Reveal: What secret does this person know that they do not want revealed?",
                    "Reveal: What concealed items or objects do they possess?",
                    "Reveal: What supernatural abilities or powers do they possess?",
                    "Reveal: What is their true intention regarding me or my objective?",
                ],
                optionsOn: OptionBands.All)),
            new(6, new(LunarBarrage,
                "When you draw your bow and channel Luna's power into your shot, roll +DEX. **10+:** Target up to 3 enemies. **7-9:** Up to 2. " +
                "**6-:** 1 enemy, but the GM makes a move.\nBefore releasing, roll 1d10 damage. On a 6 or higher, roll another d10 and add it; keep going " +
                "while each new roll is 6+. Every target takes the full total. Blood Moon can't be used with Lunar Barrage. Use /subclass luna lunar-barrage")),
            new(9, new(LifeForLife,
                "When you invoke Luna against an enemy whose level is equal to or lower than your own, you may offer your life for theirs. The enemy " +
                "is instantly killed. You're reduced to 0 HP and must roll Last Breath; no damage is rolled. Boon of Fortune applies to this roll. " +
                "Use /subclass luna life-for-life")),
            new(10, new("???",
                "L̷E̸V̶E̷L̴ 1̵0̸\n▓▒░ █▓̷█̸▓̶░▒▓ ??? ▓▒░█̷▓̸█░▒▓\nU̴N̵L̸O̷C̶K̷: █̸█̴█̶█̵█̷█̸█̵█̶\nS̴T̶A̸T̷U̴S̷: ▓▓▒░ █̴█̸█̴█̷ ░▒▓▓"))
            {
                Hidden = true,
            },
        ])
    {
        Bonuses = VowBonuses,
        Status = c => ActiveVowOf(c) is { } vow ? $"**{vow.BoonName}**: {vow.Objective}" : null,
        StateAbilities = [ActiveVow],
        CommandHint = "Use /subclass luna for Blood Moon, your Vow, and Luna's other powers. Roll Revelation with /move class.",
    };

    [GeneratedRegex(@"^\*\*Boon of (?<boon>\w+)(?: \((?<stat>\w+)\))?\*\*\s*\nObjective: (?<objective>.+)$", RegexOptions.Singleline)]
    private static partial Regex StoredVow();

    /// <summary>
    /// How a Vow is written into the Active Vow ability's description.
    /// </summary>
    public static string Store(LunaVow vow) => $"**{vow.BoonName}**\nObjective: {vow.Objective}";

    public static LunaVow? ActiveVowOf(Character character)
    {
        var ability = character.Abilities?.FirstOrDefault(a => a.Name.Equals(ActiveVow, StringComparison.OrdinalIgnoreCase));
        if (ability?.Description is not { } description || StoredVow().Match(description) is not { Success: true } match)
            return null;

        if (!Enum.TryParse<LunaBoon>(match.Groups["boon"].Value, ignoreCase: true, out var boon))
            return null;

        StatType? stat = Enum.TryParse<StatType>(match.Groups["stat"].Value, ignoreCase: true, out var parsed) ? parsed : null;
        return new LunaVow(boon, stat, match.Groups["objective"].Value.Trim());
    }

    /// <summary>
    /// What a Boon gives its Champion and what its Echo gives everyone else.
    /// </summary>
    public static (string Champion, string Echo) Describe(LunaVow vow)
    {
        var stat = vow.Stat?.ToString().ToUpper() ?? "the chosen stat";
        return vow.Boon switch
        {
            LunaBoon.Wrath => ("+6 damage", "+3 damage"),
            LunaBoon.Aptitude => ($"+2 to {stat}", $"+1 to {stat}"),
            LunaBoon.Veil => (
                "You can see and physically interact with magical constructs and effects as though they were tangible, and try to disarm, disrupt, or dispel them even when you normally couldn't.",
                "Party members near you can see magical effects that would normally be invisible and recognise when something is being affected by magic."),
            _ => ("+2 to Last Breath", "+1 to Last Breath"),
        };
    }

    /// <summary>
    /// The Boon's full power for the Champion, and its Echo for everyone else in the party.
    /// </summary>
    private static IEnumerable<RollBonus> VowBonuses(Character owner, Character roller, RollKind kind, StatType? stat)
    {
        if (ActiveVowOf(owner) is not { } vow)
            yield break;

        var isChampion = owner.ID == roller.ID;
        var applies = vow.Boon switch
        {
            LunaBoon.Wrath => kind == RollKind.Damage,
            LunaBoon.Aptitude => kind == RollKind.Stat && stat is not null && stat == vow.Stat,
            LunaBoon.Fortune => kind == RollKind.LastBreath,
            _ => false,
        };
        if (!applies)
            yield break;

        var (champion, echo) = vow.Boon == LunaBoon.Wrath ? (6, 3) : (2, 1);
        yield return isChampion
            ? new RollBonus($"Boon of {vow.Boon} (your Vow)", champion)
            : new RollBonus($"Echo of {vow.Boon} ({owner.Name}'s Vow)", echo);
    }

    /// <summary>
    /// How much of a Blood Moon sacrifice comes back.
    /// </summary>
    public static int Recovered(int sacrificed, MoveOutcome outcome) => outcome switch
    {
        MoveOutcome.StrongHit => sacrificed,
        MoveOutcome.WeakHit => sacrificed / 2,
        _ => 0,
    };

    /// <summary>
    /// Lunar Barrage's damage: a d10, rolling again and adding while each roll is 6 or higher.
    /// </summary>
    /// <param name="rollD10">Rolls one d10.</param>
    /// <returns>Every d10 rolled, in order.</returns>
    public static IReadOnlyList<int> RollBarrage(Func<int> rollD10)
    {
        var rolls = new List<int>();
        do
        {
            rolls.Add(rollD10());
        } while (rolls[^1] >= BarrageExplodesOn && rolls.Count < MaxBarrageRolls);

        return rolls;
    }

    /// <summary>
    /// How many enemies Lunar Barrage can target.
    /// </summary>
    public static int BarrageTargets(MoveOutcome outcome) => outcome switch
    {
        MoveOutcome.StrongHit => 3,
        MoveOutcome.WeakHit => 2,
        _ => 1,
    };
}
