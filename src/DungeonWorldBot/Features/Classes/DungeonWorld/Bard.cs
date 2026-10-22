using DungeonWorldBot.Data.Entities;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.DungeonWorld;

/// <summary>
/// Summarised from the Dungeon World SRD by Sage LaTorra and Adam Koebel (CC BY 3.0).
/// </summary>
public static class Bard
{
    private static ClassMove Lore(string area) =>
        new($"Bardic Lore: {area}", $"When you first meet an important creature, location or item (your call) covered by {area}, ask the GM one question about it; they answer truthfully. The GM may ask what tale, song or legend you heard it in.");

    public static readonly ClassSheet Sheet = new(
        ClassType.Bard,
        "Dungeon World",
        new ClassColor("#b0457a", "#ec8ab8"),
        startingMoves:
        [
            Rolled("Arcane Art", StatType.Cha,
                "When you weave a performance into a basic spell, choose an ally and an effect",
                "The ally gets the effect.",
                "Your spell still works, but you draw unwanted attention or your magic reverberates to other targets too (GM's choice).",
                note: "Effects:\n• Heal 1d8 damage\n• +1d4 forward to damage\n• Their mind is shaken clear of one enchantment\n• The next time someone successfully aids them, they get +2 instead of +1"),
            new("A Port in the Storm", "When you return to a civilized settlement you've visited before, tell the GM when you were last there. They tell you how it's changed since."),
            new("Charming and Open",
                "When you speak frankly with someone, ask their player a question from the list; they answer truthfully, then may ask you one back:\n" +
                "• Whom do you serve?\n• What do you wish I would do?\n• How can I get you to ___?\n• What are you really feeling right now?\n• What do you most desire?"),
        ],
        features:
        [
            new ClassFeature("Race Move", "Which race move do you have?",
            [
                new("Race: Human", "When you first enter a civilized settlement, someone who respects the custom of hospitality to minstrels takes you in as their guest."),
                new("Race: Elf", "When you enter an important location (your call), ask the GM for one fact from its history."),
            ]),
            new ClassFeature("Bardic Lore", "What's your area of expertise?",
            [
                Lore("Spells and Magicks"), Lore("The Dead and Undead"), Lore("Grand Histories of the Known World"),
                Lore("A Bestiary of Creatures Unusual"), Lore("The Planar Spheres"), Lore("Legends of Heroes Past"),
                Lore("Gods and Their Servants"),
            ]),
        ],
        level2To5:
        [
            new("Healing Song", "When you heal with Arcane Art, heal +1d8 damage."),
            new("Vicious Cacophony", "When you grant bonus damage with Arcane Art, grant an extra +1d4 damage."),
            Rolled("It Goes to Eleven", StatType.Cha,
                "When you unleash a crazed performance (a righteous lute solo, a mighty brass blast), choose a target who can hear you",
                "The target attacks their nearest ally in range.",
                "They attack their nearest ally, but you also draw their attention and ire."),
            Rolled("Metal Hurlant", StatType.Con,
                "When you shout with great force or play a shattering note, choose a target",
                "The target takes 1d10 damage and is deafened for a few minutes.",
                "You still damage the target, but it's out of control: the GM picks an extra target nearby."),
            new("A Little Help from My Friends", "When you successfully Aid someone, you take +1 forward too."),
            new("Eldritch Tones", "Your Arcane Art is strong: choose two effects instead of one."),
            new("Duelist's Parry", "When you Hack and Slash, take +1 armor forward."),
            new("Bamboozle", "When you Parley with someone, on a 7+ you also take +1 forward with them."),
            new("Multiclass Dabbler", "Get one move from another class, treating your level as one lower.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true) },
            new("Multiclass Initiate", "Get one move from another class, treating your level as one lower.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true) },
        ],
        level6To10:
        [
            new("Healing Chorus", "When you heal with Arcane Art, heal +2d8 damage.") { Replaces = "Healing Song" },
            new("Vicious Blast", "When you grant bonus damage with Arcane Art, grant an extra +2d4 damage.") { Replaces = "Vicious Cacophony" },
            new("Unforgettable Face", "When you meet someone you've met before (your call) after some time apart, take +1 forward against them."),
            Rolled("Reputation", StatType.Cha,
                "When you first meet someone who's heard songs about you",
                "Tell the GM two things they've heard about you.",
                "Tell the GM one thing they've heard, and the GM tells you one."),
            new("Eldritch Chord", "When you use Arcane Art, choose two effects, and choose one of them to double.") { Replaces = "Eldritch Tones" },
            new("An Ear for Magic", "When you hear an enemy cast a spell, the GM tells you its name and effects. Take +1 forward acting on the answers."),
            new("Devious", "When you use Charming and Open, you can also ask \"How are you vulnerable to me?\" Your subject can't ask you this."),
            new("Duelist's Block", "When you Hack and Slash, take +2 armor forward.") { Replaces = "Duelist's Parry" },
            new("Con", "When you Parley with someone, on a 7+ you also take +1 forward with them and ask their player one question they must answer truthfully.") { Replaces = "Bamboozle" },
            new("Multiclass Master", "Get one move from another class, treating your level as one lower.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true) },
        ]);
}
