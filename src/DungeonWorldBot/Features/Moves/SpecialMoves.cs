using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;

namespace DungeonWorldBot.Features.Moves;

/// <summary>
/// The jobs on a Perilous Journey, each rolled by a different member of the party.
/// </summary>
public enum JourneyRole
{
    Trailblazer,
    Scout,
    Quartermaster,
}

/// <summary>
/// How long a character spent preparing for Bolster.
/// </summary>
public enum BolsterLength
{
    Week,
    Month,
}

/// <summary>
/// The rolled special moves for camping, travel and time in town, summarized from the Dungeon World SRD (CC BY 3.0).
/// Make Camp and Recover don't roll, so they live in <see cref="SpecialMoveRules"/>.
/// </summary>
public static class SpecialMoves
{
    public static readonly MoveDefinition TakeWatch = new(
        "Take Watch",
        StatType.Wis,
        "When you're on watch and something approaches the camp",
        StrongHit: "You're able to wake the camp and prepare a response. Everyone in the camp takes +1 forward.",
        WeakHit: "You react just in time. You may wake the camp, but no one gets +1 forward.",
        Miss: "Whatever approaches has the drop on you.");

    private const string JourneyTrigger = "When you travel through hostile territory";
    private const string JourneyWeakHit = "You do your job as expected: the normal number of rations are used, the journey takes about as long as expected, and no one gets the drop on you, but you don't get the drop on them either.";

    public static readonly MoveDefinition Trailblazer = new(
        "Perilous Journey: Trailblazer",
        StatType.Wis,
        JourneyTrigger,
        StrongHit: "You cut the time it takes to reach your destination. The GM will say by how much.",
        WeakHit: JourneyWeakHit,
        Miss: MoveDefinition.DefaultMiss);

    public static readonly MoveDefinition Scout = new(
        "Perilous Journey: Scout",
        StatType.Wis,
        JourneyTrigger,
        StrongHit: "You spot any trouble quickly enough to let the party get the drop on it.",
        WeakHit: JourneyWeakHit,
        Miss: MoveDefinition.DefaultMiss);

    public static readonly MoveDefinition Quartermaster = new(
        "Perilous Journey: Quartermaster",
        StatType.Wis,
        JourneyTrigger,
        StrongHit: "You cut the rations the journey needs by one.",
        WeakHit: JourneyWeakHit,
        Miss: MoveDefinition.DefaultMiss);

    public static readonly MoveDefinition Supply = new(
        "Supply",
        StatType.Cha,
        "When you go to buy something special, beyond what's usually available here, or non-mundane",
        StrongHit: "You find what you're looking for at a fair price.",
        WeakHit: "You'll have to pay more or settle for something that's not exactly what you wanted, but close. The GM chooses which.",
        Miss: MoveDefinition.DefaultMiss);

    public static readonly MoveDefinition Carouse = new(
        "Carouse",
        Stat: null,
        $"When you return triumphant and throw a big party, spend {SpecialMoveRules.CarouseCost} coin (roll + each extra {SpecialMoveRules.CarouseCost} spent)",
        StrongHit: "Choose 3:",
        WeakHit: "Choose 1:",
        Miss: "You still choose 1, but things get really out of hand. The GM will say how.",
        Options:
        [
            "You befriend a useful NPC",
            "You hear rumors of an opportunity",
            "You gain useful information",
            "You are not entangled, ensorcelled, or tricked",
        ],
        OptionsOn: OptionBands.All);

    public static readonly MoveDefinition OutstandingWarrants = new(
        "Outstanding Warrants",
        StatType.Cha,
        "When you return to a civilized place in which you've caused trouble before",
        StrongHit: "Word has spread of your deeds and everyone recognizes you.",
        WeakHit: "Word has spread of your deeds and everyone recognizes you. The GM also chooses a complication:",
        Miss: "The GM makes a move.",
        Options:
        [
            "The local constabulary has a warrant out for your arrest",
            "Someone has put a price on your head",
            "Someone important to you has been put in a bad spot as a result of your actions",
        ],
        OptionsOn: OptionBands.WeakHit);

    /// <summary>
    /// Every rolled special move. Static fields set up in order, so this comes after them.
    /// </summary>
    public static readonly IReadOnlyList<MoveDefinition> All =
    [
        TakeWatch, Trailblazer, Scout, Quartermaster, Supply, Carouse, OutstandingWarrants,
    ];

    public static MoveDefinition PerilousJourney(JourneyRole role) => role switch
    {
        JourneyRole.Trailblazer => Trailblazer,
        JourneyRole.Scout => Scout,
        _ => Quartermaster,
    };
}

/// <summary>
/// The numbers behind the special moves that don't roll.
/// </summary>
public static class SpecialMoveRules
{
    public const int CarouseCost = 100;

    /// <summary>
    /// Bolster: a week or more of preparing takes 1, a month or longer takes 3.
    /// </summary>
    public static readonly ClassResource Preparation = new(
        "Preparation",
        "From Bolster: spend 1 for +1 to any roll, only one per roll. Take it with /downtime bolster")
    {
        ResetWithAll = false,
    };

    public static int PreparationFor(BolsterLength length) => length == BolsterLength.Month ? 3 : 1;

    /// <summary>
    /// Make Camp: waking from a few uninterrupted hours of sleep heals half your max HP.
    /// </summary>
    public static int HealOnWaking(Character character) => character.Health.MaxHP / 2;

    /// <summary>
    /// Recover: one debility every three days of rest, or every two under a healer's care.
    /// </summary>
    public static int DebilitiesRecovered(int days, bool healer) => healer ? days / 2 : days / 3;

    /// <summary>
    /// The rations to eat from when making camp: the biggest stack of anything called a ration.
    /// </summary>
    public static Item? Rations(Character character) =>
        character.Inventory.Items?
            .Where(i => i.Amount > 0 && i.Name.Contains("ration", StringComparison.OrdinalIgnoreCase))
            .MaxBy(i => i.Amount);

    public static Item? Coins(Character character) =>
        character.Inventory.Items?.FirstOrDefault(i => i.Name.Equals("Coins", StringComparison.OrdinalIgnoreCase));
}
