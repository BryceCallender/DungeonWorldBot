using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.GrimWorld;

public static class Shaman
{
    private const string FirstSpiritTotem = "Totem of the First Spirits";

    private static ClassMove GuardianSpirit(string name, string effect) =>
        new($"Guardian Spirit: {name}", $"Once per attack, when you deal damage and a die shows 1, your spirit steps in: reroll that die and {effect}.");

    // Totems are tracked as abilities whose uses are the totem's charges
    private static ClassMove WorldlySpirit(string name, string word, string summary) =>
        new(name, $"*\"{word}\"*: {summary}") { Uses = 1 };

    private static ClassMove FirstSpirit(string name, string word, string summary) =>
        new(name, $"*\"{word}\"*: {summary} (First Spirit totems always shatter when released.)") { Uses = 1, Requires = FirstSpiritTotem };

    public static readonly ClassSheet Sheet = new(
        ClassType.Shaman,
        "Grim World",
        new ClassColor("#16806e", "#4ac6ae"),
        startingMoves:
        [
            new("Spirit Pact", "You can keep up to 3 Worldly Spirit Totems. Describe what your totems look like."),
            new("Totem Creation", "Spend an uninterrupted hour or so crafting a totem to bind a Worldly Spirit (release one first if you're at your limit). Each totem starts with 1 Charge; saying the spirit's name releases it and uses a Charge. At 0 Charges the totem shatters for good."),
            Rolled("Spirit Talk", StatType.Wis,
                "When you say a bound spirit's name and try to keep its totem's Charge",
                "The spirit is released and returns to its totem afterwards.",
                "The spirit is released, but choose one:",
                options:
                [
                    "You draw unwelcome attention or are put in a spot (the GM says how)",
                    "-1 ongoing to Spirit Talk until you spend an hour honoring the spirits",
                    "The totem still loses a Charge",
                ],
                optionsOn: OptionBands.WeakHit),
            Rolled("Spirit Catcher", StatType.Wis,
                "When you witness a creature die and bind its spirit to your Spirit Catcher totem",
                "It's captured, and the GM tells you the supernatural effect it has when released.",
                "It's captured, but the GM adds a complication: it's unpredictable, has an unwanted side effect, or must be used soon.",
                note: "The Spirit Catcher has no Charges, but the spirit moves on once released (no Spirit Talk). You only ever have one."),
            new("Spiritual Adviser", "You can give your totems to allies. Describe them, say how many Charges they hold, and teach them the spirit's name. Only you can Spirit Talk."),
            new("Murmur", "When you Discern Realities, you can also ask: \"What do the spirits whisper here?\""),
        ],
        features:
        [
            new ClassFeature("Guardian Spirit", "Which Guardian Spirit watches over you?",
            [
                GuardianSpirit("Boar", "add the Forceful tag to the attack"),
                GuardianSpirit("Beetle", "take +2 armor forward"),
                GuardianSpirit("Falcon", "take +1 forward on your next attack"),
                GuardianSpirit("Irontree", "take +1 forward to Defy Danger"),
            ]),
        ],
        level2To5:
        [
            new("Chain Lightning", "Your Lightning Spirit can jump to a nearby target. Roll an extra 1d6 and decide which die hits which target."),
            new("Wise Counsel", "When you show someone the wiser path, you can Parley with WIS instead of CHA."),
            new("Web of Whispers", "Whisper Wind totems don't count against your limit, and whispering into one is heard from all of them."),
            new("Wandering Spirit", "Your pact allows 1 more totem, but the GM chooses the spirit (likely one you rarely use)."),
            new("Ancestral Healing", "Your Spirit of Mending heals an extra amount equal to your level."),
            new("Séance", "You can Spirit Talk when releasing your Spirit Catcher totem, as if it were a normal totem."),
            new("Inciter", "On a 10+ Spirit Talk, you can take a 7-9 option to double the spirit's effect or its number of targets."),
            new("Totemic Defense", "When you take damage, say the name of a bound spirit. It blocks the damage, but its totem is destroyed."),
            new("Spirit Guide", "Your pact allows 1 more totem, but it must be given to and used by someone else. Using it yourself breaks the pact."),
            new("Vision Quest", "Spend a night alone in nature with a vision-enhancing substance and tell the spirits what troubles you. They offer guidance; take +1 forward acting on it."),
        ],
        level6To10:
        [
            new(FirstSpiritTotem, "Spend about a day crafting a grand totem to bind a First Spirit. It has 1 Charge and always shatters when released (no Spirit Talk). You can only have one at a time, and calling on them too often draws the spirit realm's demands."),
            new("Totemic Weapon", "Bind a Worldly Spirit to a weapon you carry. On a 10+ Hack and Slash or Volley with it, you can release the spirit for free."),
            new("Medium", "When you first meet a noteworthy person, the GM tells you what the nearby spirits whisper about them."),
            new("Maker of Talismans", "Using a rare or valuable item, turn a totem into a talisman with 2 Charges. An empty talisman rests instead of shattering, and refills at dawn or after an hour-long purification."),
            new("Spirit Walkers", "When you and others sit in a circle around a fire, you can leave your bodies and roam the world as spirits for as long as it burns."),
            new("Soulgaze", "When you and someone hold each other's gaze, reveal something meaningful about yourself (a secret, desire or intention). The GM reveals something equally meaningful about them."),
            new("Flame Spirit", "Inhale the spirit of a campfire or torch flame (putting it out) and hold it under your tongue. Spit it out at any time to deal your damage and set your target on fire."),
            new("Protector", "Your Guardian Spirit also protects anyone you have a bond with, when they roll a 1 on a damage die."),
        ],
        deathMove: new("Death: The Last Totem",
            "When you die (failing or forgoing Last Breath), your totems shatter and a cocoon of spirit energy gathers near your body. When it fades, it leaves a powerful magic item behind. Work with the GM to create it around what you stood for in life."),
        extras:
        [
            WorldlySpirit("Spirit of Mending", "naandawi", "Heals you or a nearby ally for 1d8."),
            WorldlySpirit("Lightning Spirit", "yuka-oom", "Lightning hits a nearby target for 1d6 damage, ignoring armor unless they're made of something resistant like stone or wood."),
            WorldlySpirit("Spirit of the Python", "chu'a", "A large snake wraps the target so only its mouth can move, until it takes damage or you call the snake off. Powerful creatures may break free, taking 1d6 damage."),
            WorldlySpirit("Stubborn Turtle Spirit", "mishi-mishi", "The speaker gains armor equal to your WIS modifier against attacks from behind, until the battle ends."),
            WorldlySpirit("Prickly Porcupine Spirit", "paheen", "Until the battle ends, anyone who hits the speaker in melee takes damage equal to your WIS modifier (ignores armor)."),
            WorldlySpirit("Spirit of the Owl", "suu'ak", "The owl stays near you: +1 to Discern Realities. On a 6-, it rerolls the failure, then disappears."),
            WorldlySpirit("Playful Tiger Spirit", "amotekun", "The tiger stalks a target and pounces to stop every ranged attack it tries, until it dies or you call the tiger away."),
            WorldlySpirit("Ornery Donkey Spirit", "balubera", "Carries 10 load. Feed it something tasty every day or it leaves."),
            WorldlySpirit("Whisper Wind Spirit", "madweyaashi", "Your whispers come out of this totem, however far away it is. Release and rebind it weekly or it grows restless."),
            WorldlySpirit("Spirit of Desire", "telakapa", "Point the totem at someone: they'll do anything to touch it. Once they do, the spirit releases and the urge fades."),
            FirstSpirit("Spirit of the First Earthformer", "senu-taahi", "The ground rumbles and a volcano forms, erupting lava and huge rocks everywhere nearby."),
            FirstSpirit("Spirit of First Love", "senu-nayok", "Touch it to someone and speak a name they know: they fall permanently, naively in love with that person. Hearts can still break."),
            FirstSpirit("Spirit of First Fear", "senu-goshi", "Bind it naming someone; seeing the totem fills them with terror. Released, it haunts them until called off or exorcised."),
            FirstSpirit("Spirit of the First Trade", "senu-genv", "Touch it to someone and swap something of theirs (face, ability, even spirit) for your equivalent. Destroying the totem reverses it."),
            FirstSpirit("Spirits of the First Wolves", "senu-hoka", "A spirit wolf pack hunts a chosen target anywhere. Howl to learn their direction; they vanish when you catch up."),
            FirstSpirit("Spirit of the First Harvest", "senu-lesqua", "Every nearby crop ripens at once, or something edible bursts from the ground."),
            FirstSpirit("Spirit of the First Winter", "senu-wanber", "The temperature plummets and snow covers the land. Those vulnerable to cold are Clumsy (-1 ongoing) until they warm up."),
            FirstSpirit("Spirit of the First Spring", "senu-doona", "Plants as far as you can see flourish, and every nearby creature heals 2d8."),
            FirstSpirit("Spirit of the First Sun", "senu-sakuun", "The sun leaps to its peak: water evaporates, plants wither, and anyone in direct sunlight risks severe burns."),
            FirstSpirit("Spirit of the First Storm", "senu-yuka", "A huge storm of rain, hail and lightning, which you can roughly direct at targets or structures."),
            FirstSpirit("Spirit of First Home", "senu-jati", "Bind it somewhere safe and familiar. Releasing it drags everyone touching the totem back there through the spirit realm."),
        ]);
}
