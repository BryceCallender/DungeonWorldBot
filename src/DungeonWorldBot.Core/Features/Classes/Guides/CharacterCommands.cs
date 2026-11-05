using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Gear;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Spells;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Features.Subclasses.Homebrew;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Classes.Guides;

/// <summary>
/// The commands a character can use right now, grouped like a class guide: their moves, then what their class,
/// subclass, gear and spells add, then downtime. Only what they've unlocked is listed.
/// </summary>
public static class CharacterCommands
{
    /// <summary>
    /// A subclass command, and the subclass move it needs. Null when being in the subclass is enough.
    /// </summary>
    private record SubclassCommand(string Command, string? Needs = null);

    private static readonly Dictionary<string, SubclassCommand[]> SubclassCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        [ChampionOfLuna.Name] =
        [
            new("/luna vow", ChampionOfLuna.LunasVow),
            new("/luna vow-end", ChampionOfLuna.LunasVow),
            new("/luna blood-moon", ChampionOfLuna.BloodMoon),
            new("/luna lunar-barrage", ChampionOfLuna.LunarBarrage),
            new("/luna life-for-life", ChampionOfLuna.LifeForLife),
        ],
        [TacticalGuardian.Name] =
        [
            new("/guardian read-the-room", TacticalGuardian.ReadTheRoom),
            new("/guardian pool", TacticalGuardian.ReadTheRoom),
            new("/guardian one-step-ahead", TacticalGuardian.OneStepAhead),
            new("/guardian take-hit", TacticalGuardian.ReadTheRoom),
            new("/guardian release", TacticalGuardian.ShieldedPursuit),
            new("/guardian perfected-assault", TacticalGuardian.PerfectedAssault),
            new("/guardian end-fight", TacticalGuardian.ReadTheRoom),
        ],
        [CircleOfTheEclipse.Name] = [new("/eclipse aspect")],
    };

    public static IReadOnlyList<GuideStep> For(Character character)
    {
        var known = ClassSheetLibrary.KnownMoves(character);
        bool Knows(string move) => known.Contains(move, StringComparer.OrdinalIgnoreCase);

        List<GuideStep> steps = [ClassGuides.Basics];

        var rolled = CharacterMoves.KnownRolledClassMoves(character).Where(m => m.Name != ShapeshiftRules.Shapeshifter).ToList();
        steps.Add(new GuideStep("Class",
            rolled.Count > 0
                ? "Roll your class moves with `/move class` and pick the move. Read any move with `/class info`."
                : "None of your moves are rolled, so read them with `/class info`.",
            [
                .. rolled.Select(m => new GuideCommand("/move class", m.Name, ClassGuides.RollsWith(m))),
                new("/class sheet"), new("/class info"), new("/class moves"), new("/class learn"),
            ]));

        if (TacticRules.UsesTactics(character))
        {
            steps.Add(new GuideStep("Tactics", "Your active Tactic is applied to your moves for you.",
                [new("/tactic show"), new("/tactic change"), new("/tactic default")]));
        }

        if (ShapeshiftRules.CanShapeshift(character))
        {
            steps.Add(new GuideStep("Shapeshifting", "`/shapeshift into` rolls Shapeshifter; spend the hold on your form's moves.",
                [
                    new("/shapeshift into", Note: "+WIS"), new("/shapeshift move"), new("/shapeshift revert"),
                    new("/shapeshift forms"), new("/shapeshift learn"), new("/shapeshift edit"), new("/shapeshift forget"),
                ]));
        }

        // Caster levels and shapeshift hold are kept by /spell and /shapeshift, not spent by hand
        var casting = SpellCasting.Access(character);
        var kept = casting.Select(a => a.LevelResource.Name).Append(ShapeshiftRules.Hold.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var resources = ClassSheetLibrary.ResourcesOf(character).Where(r => !kept.Contains(r.Name)).ToList();
        if (resources.Count > 0)
        {
            steps.Add(new GuideStep("Resources", "Gain and spend your class resources as your moves say.",
                [
                    new("/resource show"),
                    .. resources.SelectMany(r => new GuideCommand[] { new("/resource gain", r.Name), new("/resource spend", r.Name) }),
                    new("/resource reset"),
                ]));
        }

        if (casting.Count > 0)
        {
            steps.Add(new GuideStep("Spells", $"You cast {string.Join(" and ", casting.Select(a => a.List))} spells.",
                [new("/spell known"), new("/spell learn"), new("/spell clear"), new("/spell list"), new("/spell info")]));
        }

        foreach (var owned in GearRules.Owned(character))
        {
            var gear = owned.Gear;
            List<GuideCommand> commands = [new("/gear info", gear.Name), new("/gear socket", Note: gear.ModeKind)];
            if (gear.Modes.Any(m => m.Counter is not null))
                commands.Add(new("/gear counter"));
            if (gear.Modes.Any(m => m.Action is not null))
                commands.Add(new("/gear action"));
            commands.Add(new("/gear end-fight"));

            steps.Add(new GuideStep(gear.Name, $"Switch its {gear.ModeKind.ToLower()} with `/gear socket`.", commands));
        }

        if (SubclassLibrary.Of(character) is { } subclass)
        {
            var own = SubclassCommands.GetValueOrDefault(subclass.Name, [])
                .Where(c => c.Needs is null || Knows(c.Needs))
                .Select(c => new GuideCommand(c.Command))
                .ToList();

            steps.Add(new GuideStep(subclass.Name, subclass.CommandHint ?? "Roll your subclass moves with `/move class`.",
                [.. own, new("/subclass info", subclass.Name)]));
        }

        steps.Add(ClassGuides.Downtime);

        return steps;
    }
}
