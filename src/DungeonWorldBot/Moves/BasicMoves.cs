using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Moves;

/// <summary>
/// The basic moves every character has, summarised from the Dungeon World SRD (CC BY 3.0).
/// </summary>
public static class BasicMoves
{
    public static readonly MoveDefinition HackAndSlash = new(
        "Hack and Slash",
        StatType.Str,
        "When you attack an enemy in melee",
        StrongHit: "You deal your damage to the enemy and avoid their attack. Or, you can deal +1d6 damage but expose yourself to the enemy's attack.",
        WeakHit: "You deal your damage to the enemy and the enemy makes an attack against you.",
        Miss: MoveDefinition.DefaultMiss,
        RollsDamage: true);

    public static readonly MoveDefinition Volley = new(
        "Volley",
        StatType.Dex,
        "When you take aim and shoot at an enemy at range",
        StrongHit: "You have a clear shot—deal your damage.",
        WeakHit: "Choose one (whichever you choose, you deal your damage):",
        Miss: MoveDefinition.DefaultMiss,
        Options:
        [
            "You have to move to get the shot, placing you in danger of the GM's choice",
            "You have to take what you can get: -1d6 damage",
            "You have to take several shots, reducing your ammo by one",
        ],
        RollsDamage: true,
        OptionsOn: OptionBands.WeakHit);

    public static readonly MoveDefinition DefyDanger = new(
        "Defy Danger",
        Stat: null,
        "When you act despite an imminent threat or suffer a calamity",
        StrongHit: "You do what you set out to, the threat doesn't come to bear.",
        WeakHit: "You stumble, hesitate, or flinch: the GM will offer you a worse outcome, hard bargain, or ugly choice.",
        Miss: MoveDefinition.DefaultMiss);

    public static readonly MoveDefinition Defend = new(
        "Defend",
        StatType.Con,
        "When you stand in defense of a person, item, or location under attack",
        StrongHit: "Hold 3. As long as you stand in defense, spend hold when the target is attacked:",
        WeakHit: "Hold 1. As long as you stand in defense, spend hold when the target is attacked:",
        Miss: MoveDefinition.DefaultMiss,
        Options:
        [
            "Redirect an attack from the thing you defend to yourself",
            "Halve the attack's effect or damage",
            "Open up the attacker to an ally giving that ally +1 forward against the attacker",
            "Deal damage to the attacker equal to your level",
        ]);

    public static readonly MoveDefinition SpoutLore = new(
        "Spout Lore",
        StatType.Int,
        "When you consult your accumulated knowledge about something",
        StrongHit: "The GM will tell you something interesting and useful about the subject relevant to your situation.",
        WeakHit: "The GM will only tell you something interesting—it's on you to make it useful.",
        Miss: MoveDefinition.DefaultMiss);

    public static readonly MoveDefinition DiscernRealities = new(
        "Discern Realities",
        StatType.Wis,
        "When you closely study a situation or person",
        StrongHit: "Ask the GM 3 questions from the list below. Take +1 forward when acting on the answers.",
        WeakHit: "Ask the GM 1 question from the list below. Take +1 forward when acting on the answer.",
        Miss: MoveDefinition.DefaultMiss,
        Options:
        [
            "What happened here recently?",
            "What is about to happen?",
            "What should I be on the lookout for?",
            "What here is useful or valuable to me?",
            "Who's really in control here?",
            "What here is not what it appears to be?",
        ]);

    public static readonly MoveDefinition Parley = new(
        "Parley",
        StatType.Cha,
        "When you have leverage on a GM character and manipulate them",
        StrongHit: "They either do what you ask or reveal the easiest way to convince them.",
        WeakHit: "They will do what you ask, but need some concrete assurance, corroboration, or evidence first.",
        Miss: MoveDefinition.DefaultMiss);

    public static readonly MoveDefinition AidOrInterfere = new(
        "Aid or Interfere",
        Stat: null,
        "When you help or hinder someone you have a bond with (roll + bonds with them)",
        StrongHit: "They take +1 or -2 to their roll, your choice.",
        WeakHit: "They take +1 or -2 to their roll, your choice, but you also expose yourself to danger, retribution, or cost.",
        Miss: MoveDefinition.DefaultMiss);

    public static readonly MoveDefinition LastBreath = new(
        "Last Breath",
        Stat: null,
        "When you're dying you catch a glimpse of what lies beyond the Black Gates of Death's Kingdom",
        StrongHit: "You've cheated death—you're no longer dying but you're still in bad shape.",
        WeakHit: "Death will offer you a bargain. Take it and stabilize or refuse and pass beyond the Black Gates into whatever fate awaits you.",
        Miss: "Your time has come. You pass beyond the Black Gates of Death's Kingdom.");
}
