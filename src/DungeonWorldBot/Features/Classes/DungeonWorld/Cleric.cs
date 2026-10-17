using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Spells;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.DungeonWorld;

/// <summary>
/// Summarised from the Dungeon World SRD by Sage LaTorra and Adam Koebel (CC BY 3.0).
/// </summary>
public static class Cleric
{
    private const string ClericLevel = "Cleric Level";

    private static ClassMove Domain(string name) =>
        new($"Domain: {name}", $"Your deity's domain is {name}.");

    public static readonly ClassSheet Sheet = new(
        ClassType.Cleric,
        "Dungeon World",
        startingMoves:
        [
            new("Divine Guidance", "When you petition your deity according to the precept of your religion, you're granted some useful knowledge or boon related to your deity's domain. The GM tells you what."),
            Rolled("Turn Undead", StatType.Wis,
                "When you hold your holy symbol aloft and call on your deity for protection",
                "So long as you keep praying and brandishing your symbol, no undead may come within reach of you. You also daze intelligent undead and make mindless undead flee. Aggression breaks these effects.",
                "So long as you keep praying and brandishing your symbol, no undead may come within reach of you."),
            new("Commune",
                "When you spend uninterrupted time (an hour or so) in quiet contemplation with your deity, you lose any spells already granted to you, " +
                "then are granted new spells whose total levels don't exceed your level+1, none higher than your level. Your rotes are always prepared and never count against the limit."),
            Rolled("Cast a Spell", StatType.Wis,
                "When you unleash a spell granted by your deity",
                "The spell is cast and your deity doesn't revoke it, so you can cast it again.",
                "The spell is cast, but choose one:",
                options:
                [
                    "You draw unwelcome attention or put yourself in a spot (the GM says how)",
                    "Your casting distances you from your deity: -1 ongoing to Cast a Spell until you next Commune",
                    "After it's cast, your deity revokes the spell until you Commune again",
                ],
                note: "Maintaining ongoing spells can give a penalty to this roll.") with
            {
                GrantsSpells = new(SpellList.Cleric, ClericLevel),
            },
        ],
        features:
        [
            new ClassFeature("Race Move", "Which race move do you have?",
            [
                new("Race: Human", "Your faith is diverse. Choose one wizard spell: you can be granted and cast it as if it were a cleric spell."),
                new("Race: Dwarf", "You are one with stone. When you Commune, you're also granted a special version of Words of the Unspeaking as a rote, which only works on stone."),
            ]),
            new ClassFeature("Deity", "What is your deity's domain?",
            [
                Domain("Healing and Restoration"), Domain("Bloody Conquest"), Domain("Civilization"),
                Domain("Knowledge and Hidden Things"), Domain("The Downtrodden and Forgotten"), Domain("What Lies Beneath"),
            ]),
            new ClassFeature("Precept", "What is a precept of your religion?",
            [
                new("Petition: Suffering", "Your religion preaches the sanctity of suffering."),
                new("Petition: Gaining Secrets", "Your religion is cultish and insular."),
                new("Petition: Offering", "Your religion observes important sacrificial rites."),
                new("Petition: Personal Victory", "Your religion believes in trial by combat."),
            ]),
        ],
        level2To5:
        [
            new("Chosen One", "Choose one spell. You're granted it as if it were one level lower."),
            new("Invigorate", "When you heal someone, they take +2 forward to their damage."),
            new("The Scales of Life and Death", "When someone takes their Last Breath in your presence, they take +1 to the roll."),
            new("Serenity", "When you Cast a Spell, ignore the first -1 penalty from ongoing spells."),
            new("First Aid", "Cure Light Wounds is a rote for you and doesn't count against your limit of granted spells."),
            new("Empower", "When you Cast a Spell, on a 10+ you can choose from the 7-9 list. If you do, also choose one: the spell's effects are doubled, or its targets are doubled."),
            new("Divine Intervention", "When you Commune, get 1 hold and lose any you had. Spend it when you or an ally takes damage: your deity intervenes with a fitting manifestation (a sudden gust of wind, a lucky slip, a burst of light) and negates the damage."),
            new("Penitent", "When you take damage and embrace the pain, you can take +1d4 damage (ignoring armor). If you do, take +1 forward to Cast a Spell."),
            new("Orison for Guidance", "When you sacrifice something of value to your deity and pray for guidance, your deity tells you what it would have you do. If you do it, mark XP."),
            new("Divine Protection", "When you wear no armor or shield, you have 2 armor."),
            new("Devoted Healer", "When you heal someone else of damage, add your level to the amount healed."),
        ],
        level6To10:
        [
            new("Anointed", "Choose one spell besides the one you picked for Chosen One. You're granted it as if it were one level lower.") { Requires = "Chosen One" },
            new("Apotheosis", "The first time you pray as fits your god after taking this move, choose a feature tied to your deity (rending claws, wings of sapphire feathers, an all-seeing third eye...). You gain it permanently."),
            new("Reaper", "When you take time after a conflict to dedicate your victory to your deity and deal with the dead, take +1 forward."),
            new("Providence", "Ignore the -1 penalty from up to two spells you maintain. Maintaining more gives the normal penalties.") { Replaces = "Serenity" },
            new("Greater First Aid", "Cure Moderate Wounds is a rote for you and doesn't count against your limit of granted spells.") { Requires = "First Aid" },
            new("Divine Invincibility", "When you Commune, get 2 hold and lose any you had. Spend it when you or an ally takes damage: your deity intervenes with a fitting manifestation and negates the damage.") { Replaces = "Divine Intervention" },
            new("Martyr", "When you take damage and embrace the pain, you can take +1d4 damage (ignoring armor). If you do, take +1 forward to Cast a Spell and add your level to any damage dealt or healed by the spell.") { Replaces = "Penitent" },
            new("Divine Armor", "When you wear no armor or shield, you have 3 armor.") { Replaces = "Divine Protection" },
            new("Greater Empower", "When you Cast a Spell, on a 10-11 you can choose from the 7-9 list. If you do, also choose one: double the spell's effects or its targets. On a 12+, choose one of those for free.") { Replaces = "Empower" },
            new("Multiclass Dabbler", "Get one move from another class, treating your level as one lower.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true) },
        ],
        resources:
        [
            new ClassResource(ClericLevel, "Your cleric level for spells, which follows your character level. Learn spells with /spell learn.", Start: 1)
            {
                ResetWithAll = false,
                GrowsWithLevel = true,
            },
        ]);
}
