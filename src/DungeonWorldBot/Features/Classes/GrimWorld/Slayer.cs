using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.GrimWorld;

public static class Slayer
{
    public static readonly ClassSheet Sheet = new(
        ClassType.Slayer,
        "Grim World",
        new ClassColor("#b3262e", "#f06b72"),
        startingMoves:
        [
            new("Bloodthirst", "When you roll a 6- on any move, gain 1 Thirst."),
            new("Withdrawal", "When you gain Thirst, take a debility you don't already have. With all six debilities, take 1d6 damage (ignores armor) instead."),
            new("Slayer's Strike", "When you Hack and Slash, you can roll +Thirst instead of STR or DEX."),
            new("Cathartic Kill", "When you land the killing blow on a person, monster or beast, your Thirst resets to 0 and debilities from Withdrawal fade."),
            Rolled("Slayer's Arsenal", StatType.Str,
                "When you dig through your slaying gear for something expensive, rare or unique that will help slay a creature (anything cheap or common, you just have)",
                "The GM says you have it, or something useful.",
                "The GM picks one:",
                options:
                [
                    "You have it, but it needs repairing, charging or assembling",
                    "You have it, but it has serious drawbacks",
                    "You had it, but it was lost or stolen",
                    "You don't have it, but you find a hint about where it is or how to get it",
                ],
                optionsOn: OptionBands.WeakHit),
            new("Old Friend", "An associate helps you on particularly hard hunts and is usually nearby when you really need them. Name them and decide how you met."),
        ],
        features:
        [
            new ClassFeature("Manifestation", "How does your bloodthirst manifest?",
            [
                new("Deadly Throw", "When you Volley with a thrown weapon, you can use Slayer's Strike."),
                new("Adrenaline", "When you Defy Danger with strength, you can roll +Thirst instead."),
                new("Tenacity", "Ignore the -1 from CON debilities.") { IgnoresDebility = StatType.Con },
                new("Penetrating Gaze", "When you Discern Realities on a creature you mean to slay, you can roll +Thirst instead of WIS."),
            ])
            {
                ExtraPicks = [("Useful Symptoms", 1)],
            },
            new ClassFeature("Old Friend Specialty", "What is your Old Friend's specialty?",
            [
                new("Old Friend: Librarian", "A lore expert who can research any subject. Sometimes they need time or a favor, but they always find something useful."),
                new("Old Friend: Mercenary", "A hireling with 10 points and usually no cost. Call on them too often and they'll want something, or need you to bail them out."),
                new("Old Friend: Bartender", "Knows everyone worth knowing. They can point you to whoever you're looking for, or set up a deal."),
            ])
            {
                ExtraPicks = [("Slaying Gang", 1)],
            },
        ],
        level2To5:
        [
            new("Parched", "When you use Slayer's Strike and deal damage, add your Thirst to the damage."),
            new("Useful Symptoms", "Choose another Manifestation."),
            new("Marksman", "When you Volley with a bow or crossbow, you can use Slayer's Strike."),
            new("Welcome Thirst", "When you wake in the morning, you can choose to gain 1d4 Thirst (with Withdrawal)."),
            new("Wildling", "Take a move from the Ranger list. If there's a Ranger in the party, they help you tame the beast or learn the move.") { Multiclass = MulticlassGrant.FromClasses(ClassType.Ranger) },
            new("Insomnia", "When you Take Watch, you can roll +Thirst instead. On a 10+, you also deal +1d4 damage to the approaching enemy."),
            new("Euphoric Sleep", "When you Make Camp and sleep with 0 Thirst, recover all your HP instead of half."),
            Rolled("Ensnare", StatType.Dex,
                "When you throw your bolas to ensnare an enemy",
                "They're ensnared, and can't easily untangle themselves.",
                "They're ensnared."),
            new("With Friends Like These", "You can sometimes call on monstrous or corrupted hirelings who owe you. They may have a Cost tied to their nature, like a hunger for human flesh."),
            new("Sponsor", "When an ally tries to calm your bloodthirst, roll +Bond. **10+:** halve your Thirst (round down). **7-9:** reduce your Thirst by 1."),
        ],
        level6To10:
        [
            new("I'll Sleep When You're Dead", "When Slayer's Strike fails to slay the target, your next attack against it deals +1d8 damage."),
            new("Slayer of Giants", "+1 to Defy Danger against Large or Huge enemies."),
            new("Armored Slayer", "Ignore the clumsy tag on armor."),
            new("Taste for the Hunt", "When you taste something a creature left behind (blood, hair, droppings), the GM tells you its species. Take +1 forward against it."),
            new("Slaying Gang", "Gain another Old Friend, or give your existing one another specialty."),
            new("Constriction", "On a 10+ with your bolas, also deal your damage. If that would kill the target, they fall unconscious instead.") { Requires = "Ensnare" },
            new("Signature Killer", "Gain the Fighter's Signature Weapon. If there's a Fighter in the party, they must forge it or help you get it."),
            new("Corrupted", "A close call with a virulent monster left you partly tainted. Gain a benefit (regeneration, speed, reflexes...) and a weakness (daylight, silver, holy relics...)."),
            new("Aegis Ink", "A tattoo makes you immune to one magical effect (possession, hypnosis, mind control...) while it stays intact. Describe it."),
            new("Bliss", "When a Cathartic Kill lands, heal damage equal to the Thirst you had."),
        ],
        deathMove: new("Death: Soulbound Slayer",
            "When you die (failing or forgoing Last Breath), speak a name. Your soul is bound to hunt that quarry: while it lives, you keep control of your body with a permanent 6 Thirst and need no food or sleep, but everything you do must pursue it. If your body is destroyed, you haunt the quarry as a phantom until it dies."),
        resources:
        [
            new ClassResource("Thirst", "Gained on any 6- roll; each point brings a Withdrawal debility. Resets to 0 on a Cathartic Kill.")
            {
                GainNote = "**Withdrawal:** take a debility you don't already have for each point of Thirst, with /debility add. With all six, take 1d6 damage (ignores armor) instead.",
                ResetNote = "**Cathartic Kill:** debilities from Withdrawal fade. Remove them with /debility remove, or /debility clear if they're all from Withdrawal.",
            },
        ]);
}
