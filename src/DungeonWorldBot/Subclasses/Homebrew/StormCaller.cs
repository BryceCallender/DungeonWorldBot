using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Moves;
using static DungeonWorldBot.ClassSheets.ClassMove;

namespace DungeonWorldBot.Subclasses.Homebrew;

/// <summary>
/// Storm Caller: a homebrew subclass that summons a storm and draws on its power.
/// </summary>
public static class StormCaller
{
    public const string Name = "Storm Caller";
    public const string SummonTheStorm = "Summon the Storm";
    public const string ChannelTheStorm = "Channel the Storm";
    public const string TempestAura = "Tempest Aura";
    public const string Thunderstruck = "Thunderstruck";
    public const string EyeOfTheTempest = "Eye of the Tempest";
    public const string UnleashTheTempest = "Unleash the Tempest";

    private const string NeedsStorm = "Needs a storm present; use Summon the Storm first if there isn't one.";

    public static readonly Subclass Subclass = new(
        Name,
        "Master of Wind and Thunder",
        "Homebrew",
        [],
        "Summon a storm from nothing, then channel its fury into barriers, thunder, and cataclysm.",
        [
            new(1, Rolled(SummonTheStorm, StatType.Con,
                "When you attempt to summon a storm from nothing",
                "You create a storm, small and manageable. It does what you command until you dismiss it.",
                "You create a storm, but don't fully control it. It lasts 1d4 turns before dissipating.",
                "You fail to create a storm. The energy backlashes for 1d4 damage.",
                note: "The storm is needed for every other Storm Caller move.")),
            new(1, Rolled(ChannelTheStorm, StatType.Con,
                "When you gather the energy of a nearby storm and prepare to unleash its fury",
                "You tap into its power. Choose one:",
                "Choose one, but take 1d4 backlash damage or cause collateral damage (nearby lightning strikes, wind knocking things over), " +
                "and the storm leaves unpredictable effects behind.",
                "Choose one, but take 2d4 damage as the storm lashes out. The area becomes dangerous; allies within 10 ft must make a " +
                "DEX save or take 1d6 damage.",
                options:
                [
                    "Lightning barrier: you and your allies get +1 magical armor until damaged",
                    "Storm-charged strike: +1d6 damage to your next attack",
                    "Gust of wind: knock enemies out of melee range and make a 10 ft radius difficult terrain (you describe it, the GM says how it hinders)",
                    "Storm cloud: light rain halves fire-based attacks or gives enemies relying on sight -1 forward",
                ],
                optionsOn: OptionBands.All,
                note: NeedsStorm)),
            new(3, Rolled(TempestAura, StatType.Con,
                "When you draw the storm to you, wrapping yourself in an aura of thunder and lightning",
                "Enemies within 15 ft take -1 forward. Any enemy that approaches you or strikes you in melee takes 1d6 thunder damage.",
                "The aura is weaker: enemies take 1d3 thunder damage and no -1 forward. You take 1d6 lightning damage from the backlash.",
                "You take 2d4 lightning damage and the aura turns on you and your allies. Allies within 15 ft must make a DEX save or " +
                "take 1d6 damage.",
                note: NeedsStorm)),
            new(3, Rolled(Thunderstruck, StatType.Con,
                "When you release a sudden blast of deafening thunder from the storm",
                "All enemies within 30 ft are stunned and disoriented for the next round: disadvantage on their next action and " +
                "vulnerable to your next attack.",
                "Enemies in the area take -1 forward but aren't stunned. Nearby structures and objects take 1d6 damage, and so do you.",
                "The blast rebounds for 3d6 damage to you and damages your surroundings, leaving unstable terrain or a dangerous " +
                "effect (a broken dam, a rockslide). You take -1 forward.",
                note: NeedsStorm)),
            new(6, Rolled(EyeOfTheTempest, StatType.Con,
                "When you create a calm, protective eye in the center of the storm",
                "A 20 ft safe zone gives you and your allies +3 magical armor and +1 forward on defense. Enemies in the area take -2 forward.",
                "You and your allies get +1 armor and +1 forward briefly, but the storm intensifies after 1 round. You take 1d6 damage.",
                "You take 3d6 damage and the safe zone collapses, intensifying the storm. Allies in the area must make a DEX save or " +
                "take 2d6 damage.",
                note: NeedsStorm)),
            new(9, Rolled(UnleashTheTempest, StatType.Con,
                "When you call upon the full fury of the storm",
                "A cataclysmic storm fills a 60 ft radius. All enemies take 5d6 lightning or thunder damage amid earthquakes, fires, " +
                "floods or falling debris. The area is difficult terrain for 1d6 rounds.",
                "Massive damage as above, but you take 2d6 backlash. You can't control how long the destruction lasts, and it may " +
                "leave floods or fires behind.",
                "You take 6d6 damage as the storm ravages everything. The land is permanently altered, allies in the area must roll " +
                "to avoid 1d6 damage, and you can't act next round.",
                note: "The storm dissipates afterwards, its energy spent. " + NeedsStorm)),
        ])
    {
        CommandHint = "Roll your storm moves with /move class.",
    };
}
