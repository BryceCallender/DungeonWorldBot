using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Spells;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.DungeonWorld;

/// <summary>
/// Summarised from the Dungeon World SRD by Sage LaTorra and Adam Koebel (CC BY 3.0).
/// </summary>
public static class Ranger
{
    private const string ClericLevel = "Cleric Level";

    public static readonly ClassSheet Sheet = new(
        ClassType.Ranger,
        "Dungeon World",
        new ClassColor("#2e7d4a", "#5cc27e"),
        startingMoves:
        [
            Rolled("Hunt and Track", StatType.Wis,
                "When you follow a trail of clues left by passing creatures",
                "You follow the trail until it significantly changes direction or mode of travel, and choose one:",
                "You follow the trail until it significantly changes direction or mode of travel.",
                options:
                [
                    "Learn a useful bit of information about your quarry (the GM says what)",
                    "Find out what caused the trail to end",
                ],
                optionsOn: OptionBands.StrongHit),
            Rolled("Called Shot", StatType.Dex,
                "When you attack a defenseless or surprised enemy at range, instead of just dealing your damage you can name a target",
                "The effect for your target, plus your damage.",
                "The effect for your target:",
                options:
                [
                    "Head: they do nothing but stand and drool for a few moments",
                    "Arms: they drop anything they're holding",
                    "Legs: they're hobbled and slow moving",
                ]),
            new("Animal Companion",
                "You have a supernatural bond with a loyal animal that always acts as you wish. Name it, pick a species and a base:\n" +
                "• Ferocity +2, Cunning +1, 1 Armor, Instinct +1\n• Ferocity +2, Cunning +2, 0 Armor, Instinct +1\n" +
                "• Ferocity +1, Cunning +2, 1 Armor, Instinct +1\n• Ferocity +3, Cunning +1, 1 Armor, Instinct +2\n" +
                "Pick strengths equal to its ferocity, trainings equal to its cunning (it's already trained to fight humanoids), and weaknesses equal to its instinct."),
            new("Command",
                "When you work with your animal companion on something it's trained in:\n" +
                "• Attacking the same target: add its ferocity to your damage\n• Tracking: add its cunning to your roll\n" +
                "• Taking damage: add its armor to yours\n• Discerning realities: add its cunning to your roll\n" +
                "• Parleying: add its cunning to your roll\n• Someone interferes with you: add its instinct to their roll"),
        ],
        features:
        [
            new ClassFeature("Race Move", "Which race move do you have?",
            [
                new("Race: Elf", "When you Undertake a Perilous Journey through wilderness, whatever job you take succeeds as if you rolled a 10+."),
                new("Race: Human", "When you Make Camp in a dungeon or city, you don't need to consume a ration."),
            ])
            {
                ExtraPicks = [("Half-Elven", 1)],
            },
        ],
        level2To5:
        [
            new("Half-Elven", "Gain the other race move (elf or human). Only as your first advancement."),
            new("Wild Empathy", "You can speak with and understand animals."),
            new("Familiar Prey", "When you Spout Lore about a monster, use WIS instead of INT."),
            new("Viper's Strike", "When you strike an enemy with two weapons at once, add 1d4 damage for your off-hand strike."),
            new("Camouflage", "While you keep still in natural surroundings, enemies never spot you until you move."),
            new("Man's Best Friend", "When you let your animal companion take a blow meant for you, the damage is negated and its ferocity becomes 0 (you can't do this at 0). A few hours of rest together restores it."),
            new("Blot Out the Sun", "When you Volley, spend extra ammo before rolling: each extra ammo adds a target. Roll once and apply the damage to all of them."),
            new("Well-Trained", "Choose another training for your animal companion."),
            new("God Amidst the Wastes", "Dedicate yourself to a deity. Gain the cleric's Commune and Cast a Spell moves as a level 1 cleric, gaining an effective cleric level each time you level up.") { GrantsSpells = new(SpellList.Cleric, ClericLevel) },
            new("Follow Me", "When you Undertake a Perilous Journey, you can take two roles, rolling separately for each."),
            new("A Safe Place", "When you set the watch for the night, everyone takes +1 to Take Watch."),
        ],
        level6To10:
        [
            new("Wild Speech", "You can speak with and understand any non-magical, non-planar creature.") { Replaces = "Wild Empathy" },
            new("Hunter's Prey", "When you Spout Lore about a monster, use WIS instead of INT. On a 12+, also ask the GM any one question about it.") { Replaces = "Familiar Prey" },
            new("Viper's Fangs", "When you strike an enemy with two weapons at once, add 1d8 damage for your off-hand strike.") { Replaces = "Viper's Strike" },
            new("Smaug's Belly", "When you know your target's weakest point, your arrows have 2 piercing."),
            new("Strider", "When you Undertake a Perilous Journey, you can take two roles. Roll twice and use the better result for both.") { Replaces = "Follow Me" },
            new("A Safer Place", "When you set the watch for the night, everyone takes +1 to Take Watch, and +1 forward after the night in camp.") { Replaces = "A Safe Place" },
            new("Observant", "On a hit with Hunt and Track, also ask one Discern Realities question about the creature for free."),
            new("Special Trick", "Choose a move from another class. You can use it while working with your animal companion.") { Multiclass = MulticlassGrant.AnyClass() },
            new("Unnatural Ally", "Your animal companion is a monster. Describe it. It gets +2 ferocity, +1 instinct and a new training."),
        ],
        resources:
        [
            new ClassResource(ClericLevel, "Your effective cleric level for spells: 1 at the level you took God Amidst the Wastes, +1 each level after, worked out automatically. If it's wrong, fix it once with /resource set. Learn spells with /spell learn.", Start: 1, GrantedBy: "God Amidst the Wastes")
            {
                ResetWithAll = false,
                GrowsWithLevel = true,
            },
        ]);
}
