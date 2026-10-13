using DungeonWorldBot.Data.Entities;
using static DungeonWorldBot.ClassSheets.ClassMove;

namespace DungeonWorldBot.ClassSheets.GrimWorld;

public static class Necromancer
{
    public static readonly ClassSheet Sheet = new(
        ClassType.Necromancer,
        "Grim World",
        startingMoves:
        [
            new("Replacement Parts", "Spend a few hours to stitch a body part (limb, eyeball, teeth) back on, using the original or a similar one. Apart from stitches and a slight odor, it's as good as new."),
            new("Corpse Collector", "Spend a few minutes to store a corpse of any size in one of your 3 Hexed Canopic Jars. Opening a jar drops the corpse in front of you."),
            Rolled("Raise the Dead", StatType.Int,
                "When you reanimate a nearby or stored corpse (humanoid-sized or smaller, one at a time)",
                "It rises under your control and you have 3 Power.",
                "It rises with 3 Power, but choose one:",
                options:
                [
                    "The flesh resists you: start with 1 less Power",
                    "You draw unwelcome attention or are put in a spot (the GM says how)",
                    "Magic barely holds it together: it disintegrates afterwards",
                ],
                note: "When Power runs out, the corpse collapses. You can try to raise whatever is left."),
            Rolled("Attack Command", StatType.Int,
                "When you order your Animated Corpse to do something harmful",
                "It succeeds and deals 1d6 damage.",
                "It succeeds and deals 1d6 damage, but you lose 1 Power."),
            new("Flesh Wound", "Your Animated Corpse shrugs off minor hits. When it takes a big one, the GM names an appendage coming loose; spend 1 Power to keep it, or it's destroyed."),
            new("Piece of Mind", "When you or your Animated Corpse eat part of a sentient creature's brain, the GM tells you something interesting it knew."),
            new("Curse",
                "Tell the GM what you want a curse to do. It's always possible, but the GM sets one to four conditions, like:\n" +
                "• It needs someone's blood, a lot of money, or a rare component\n• Only a lesser or limited version is possible\n" +
                "• It takes days, weeks or months to take effect\n• Someone will find out\n• You'll need to travel somewhere"),
        ],
        features:
        [
            new ClassFeature("Hexed Body Part", "Which part of your body is hexed (detachable at will)?",
            [
                new("All Seeing Eye", "When your hexed eye is detached, you can still see through it."),
                new("Lendable Ear", "When your hexed ear is detached, you can still hear through it."),
                new("Idle Finger", "When you detach your hexed finger, name something you can see. While detached, it points to that thing."),
                new("Creepy Toe", "Eat your hexed toe to heal 5 damage. It regrows in a couple of days. Describe the taste."),
            ])
            {
                ExtraPicks = [("Hexbody", 2)],
            },
        ],
        level2To5:
        [
            new("Unliving Large", "You can Raise the Dead with Large corpses."),
            new("Disposable", "On a 10+ Raise the Dead, you can treat the corpse as disposable: gain +1d4 Power, but it always disintegrates afterwards."),
            new("Monstrous", "Your Animated Corpses deal 1d8 damage. On a 12+ Attack Command, they deal 8 (no roll)."),
            new("True Collector", "Gain a special extra Hexed Canopic Jar. It can hold a corpse, or a pre-assembled Amalgamation or Abomination."),
            new("Amalgamation", "When you Raise the Dead, you can meld two corpses into one with the benefits and moves of both. If they survive, they can be split and stored again."),
            new("Dead Meat Shield", "When you take damage with your Animated Corpse nearby, it can take the hit instead, but you lose all Power."),
            new("Hexbody", "Choose 2 more Hexed Body Parts. Your teeth are hexed too: a detached tooth looks like a local coin (switch the hex on and off at will). Teeth don't grow back."),
            Rolled("Assistant", null,
                "When you give your skin-and-bone assistant (5 load, 1 HP) an order more complicated than \"follow me\"",
                "Choose one:",
                "Choose two:",
                options:
                [
                    "You have to explain your orders several times",
                    "It causes problems or draws unwanted attention (the GM says how)",
                    "It takes a lot of time and drooling",
                ]),
            new("Gravedigger",
                "When you scour a graveyard for material, you can ask the GM which dead thing here:\n" +
                "• was most powerful in life?\n• had the richest burial?\n• lived the most noteworthy life?"),
        ],
        level6To10:
        [
            new("Colossus", "You can Raise the Dead with Huge corpses.") { Requires = "Unliving Large" },
            new("Corpse Explosion", "Disposable corpses explode instead of disintegrating, dealing their damage to everyone within reach.") { Requires = "Disposable" },
            new("Mortiferous", "Your Animated Corpses deal 1d10 damage. On a 12+ Attack Command, they deal 10 (no roll).") { Replaces = "Monstrous" },
            new("Undead Pack Rat", "Gain an extra jar for spare parts. Cut up a corpse you don't need to add 1 Charge; spend 1 Charge to find a suitable replacement part for a corpse or living person."),
            new("Abomination", "You can amalgamate up to three corpses.") { Requires = "Amalgamation" },
            Rolled("Hopeless Necromantic", StatType.Int,
                "When you try to pass off your Animated Corpse as still alive",
                "Choose two:",
                "Choose one:",
                options:
                [
                    "It moves convincingly, as it did in life",
                    "It says whatever you want",
                    "It doesn't have a potent stench",
                ]),
            new("Cranium Accessories", "Beyond reattaching parts, you can add entirely new ones (fingers, eyes, whole limbs) to yourself or others. Go too far and the GM may introduce side effects."),
            new("Swarm", "You can Raise the Dead with Tiny or Small corpses as a swarm that acts together. One jar can hold a whole swarm."),
            new("Life Sap", "Choose a Blood die (d6, d8 or d10) and roll it: you or an ally takes that much damage (ignores armor). Roll it again: you or an ally heals that much."),
        ],
        deathMove: new("Death: Undead Army",
            "When you die (failing or forgoing Last Breath), the dead claw their way out of the earth and your jars burst open. Your corpse collection leads the horde in search of revenge on whoever caused your death. It lasts until your vengeance is done or the horde is destroyed."),
        resources:
        [
            new ClassResource("Power", "Gained from Raise the Dead. Spent to keep your corpse together, or lost on a 7-9 Attack Command. At 0, the corpse collapses."),
        ]);
}
