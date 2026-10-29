using DungeonWorldBot.Data.Entities;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.DungeonWorld;

/// <summary>
/// Summarized from the Dungeon World SRD by Sage LaTorra and Adam Koebel (CC BY 3.0).
/// </summary>
public static class Fighter
{
    public static readonly ClassSheet Sheet = new(
        ClassType.Fighter,
        "Dungeon World",
        new ClassColor("#5a5f6b", "#a6adbd"),
        startingMoves:
        [
            Rolled("Bend Bars, Lift Gates", StatType.Str,
                "When you use pure strength to destroy an inanimate obstacle",
                "Choose 3:",
                "Choose 2:",
                options:
                [
                    "It doesn't take a very long time",
                    "Nothing of value is damaged",
                    "It doesn't make an inordinate amount of noise",
                    "You can fix the thing again without a lot of effort",
                ]),
            new("Armored", "Ignore the clumsy tag on any armor you wear."),
            new("Signature Weapon", "This is your weapon. There are many like it, but this one is yours. Choose its base description, range, two enhancements and look. It weighs 2."),
        ],
        features:
        [
            new ClassFeature("Race Move", "Which race move do you have?",
            [
                new("Race: Human", "Once per battle, you can reroll a single damage roll (yours or someone else's)."),
                new("Race: Halfling", "When you Defy Danger and use your small size to your advantage, take +1."),
                new("Race: Elf", "Choose one weapon. You can always treat weapons of that type as if they had the precise tag."),
                new("Race: Dwarf", "When you share a drink with someone, you can Parley with them using CON instead of CHA."),
            ]),
            new ClassFeature("Weapon Base", "What is your signature weapon?",
            [
                new("Weapon: Sword", "Your signature weapon is a sword."),
                new("Weapon: Axe", "Your signature weapon is an axe."),
                new("Weapon: Hammer", "Your signature weapon is a hammer."),
                new("Weapon: Spear", "Your signature weapon is a spear."),
                new("Weapon: Flail", "Your signature weapon is a flail."),
                new("Weapon: Fists", "Your signature weapon is your fists."),
            ]),
            new ClassFeature("Weapon Range", "What range best fits your weapon?",
            [
                new("Range: Hand", "Your signature weapon has the hand tag."),
                new("Range: Close", "Your signature weapon has the close tag."),
                new("Range: Reach", "Your signature weapon has the reach tag."),
            ]),
            new ClassFeature("Weapon Enhancements", "Which two enhancements does your weapon have?",
            [
                new("Enhancement: Hooks and Spikes", "Your signature weapon deals +1 damage, but weighs 1 more.") { Damage = 1 },
                new("Enhancement: Sharp", "Your signature weapon has 2 piercing."),
                new("Enhancement: Perfectly Weighted", "Your signature weapon has the precise tag."),
                new("Enhancement: Serrated Edges", "Your signature weapon deals +1 damage.") { Damage = 1 },
                new("Enhancement: Glows", "Your signature weapon glows in the presence of one type of creature, your choice."),
                new("Enhancement: Huge", "Your signature weapon has the messy and forceful tags."),
                new("Enhancement: Versatile", "Your signature weapon has an additional range of your choice."),
                new("Enhancement: Well-Crafted", "Your signature weapon weighs 1 less."),
            ])
            {
                Picks = 2,
                ExtraPicks = [("Improved Weapon", 1)],
            },
            new ClassFeature("Weapon Look", "What does your weapon look like?",
            [
                new("Look: Ancient", "Your signature weapon is ancient."),
                new("Look: Unblemished", "Your signature weapon is unblemished."),
                new("Look: Ornate", "Your signature weapon is ornate."),
                new("Look: Blood-Stained", "Your signature weapon is blood-stained."),
                new("Look: Sinister", "Your signature weapon is sinister."),
            ]),
        ],
        level2To5:
        [
            new("Armor Mastery", "When you make your armor take the brunt of damage dealt to you, the damage is negated, but reduce the armor of your armor or shield (your choice) by 1. At 0 armor, it's destroyed."),
            new("Seeing Red", "When you Discern Realities during combat, take +1."),
            new("Interrogator", "When you Parley using threats of violence as leverage, you can use STR instead of CHA."),
            new("Scent of Blood", "When you Hack and Slash an enemy, your next attack against that same foe deals +1d4 damage."),
            new("Multiclass Dabbler", "Get one move from another class, treating your level as one lower.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true) },
            new("Merciless", "When you deal damage, deal +1d4 damage."),
            Rolled("Heirloom", StatType.Cha,
                "When you consult the spirits within your signature weapon",
                "They give you an insight into the current situation in good detail, and might ask you questions in return.",
                "They give you an impression of the current situation, and might ask you questions in return."),
            new("Improved Weapon", "Choose one extra enhancement for your signature weapon."),
            new("Blacksmith", "When you have access to a forge, you can graft the magical powers of a weapon onto your signature weapon. The magical weapon is destroyed."),
            new("Iron Hide", "You gain +1 armor."),
        ],
        level6To10:
        [
            new("Armored Perfection", "When you let your armor take the brunt of damage dealt to you, the damage is negated and you take +1 forward against the attacker, but reduce the armor of your armor or shield by 1. At 0 armor, it's destroyed.") { Replaces = "Armor Mastery" },
            Rolled("Evil Eye", StatType.Cha,
                "When you enter combat",
                "Hold 2.",
                "Hold 1.",
                miss: "Your enemies immediately identify you as their biggest threat.",
                note: "Spend hold to make eye contact with an NPC present: they freeze or flinch and can't act until you break it off.") with
            {
                Requires = "Seeing Red",
            },
            new("Taste of Blood", "When you Hack and Slash an enemy, your next attack against that same foe deals +1d8 damage.") { Replaces = "Scent of Blood" },
            new("Multiclass Initiate", "Get one move from another class, treating your level as one lower.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true) },
            new("Bloodthirsty", "When you deal damage, deal +1d8 damage.") { Replaces = "Merciless" },
            Rolled("Through Death's Eyes", StatType.Wis,
                "When you go into battle",
                "Name an NPC who will live and one who will die.",
                "Name an NPC who will live or one who will die.",
                miss: "You see your own death and take -1 ongoing throughout the battle.",
                note: "The GM makes your vision come true if it's even remotely possible."),
            new("Eye for Weaponry", "When you look over an enemy's weaponry, ask the GM how much damage they do."),
            new("Superior Warrior", "When you Hack and Slash, on a 12+ you deal your damage, avoid their attack, and impress, dismay or frighten your enemy."),
            new("Steel Hide", "You gain +2 armor.") { Replaces = "Iron Hide" },
        ]);
}
