using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Spells;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.DungeonWorld;

/// <summary>
/// Summarised from the Dungeon World SRD by Sage LaTorra and Adam Koebel (CC BY 3.0).
/// </summary>
public static class Wizard
{
    private const string WizardLevel = "Wizard Level";

    public static readonly ClassSheet Sheet = new(
        ClassType.Wizard,
        "Dungeon World",
        startingMoves:
        [
            new("Spellbook", "You start with three first level spells and all your cantrips inscribed in your spellbook. Whenever you gain a level, inscribe a new spell of your level or lower. Your spellbook weighs 1."),
            new("Prepare Spells",
                "When you spend uninterrupted time (an hour or so) in quiet contemplation of your spellbook, you lose any spells you already prepared, " +
                "then prepare spells from your spellbook whose total levels don't exceed your level+1. Your cantrips are always prepared and never count against the limit."),
            Rolled("Cast a Spell", StatType.Int,
                "When you release a spell you've prepared",
                "The spell is cast and you can cast it again later.",
                "The spell is cast, but choose one:",
                options:
                [
                    "You draw unwelcome attention or put yourself in a spot (the GM says how)",
                    "The spell disturbs the fabric of reality: -1 ongoing to Cast a Spell until you next Prepare Spells",
                    "After it's cast, the spell is forgotten until you Prepare Spells again",
                ],
                note: "Maintaining ongoing spells can give a penalty to this roll.") with
            {
                GrantsSpells = new(SpellList.Wizard, WizardLevel),
            },
            new("Spell Defense", "You can end any ongoing spell immediately and use the energy of its dissipation to deflect an oncoming attack: subtract the spell's level from the damage done to you."),
            new("Ritual",
                "When you draw on a place of power to create a magical effect, tell the GM what you're trying to achieve. It's always possible, but the GM gives you one to four conditions: " +
                "it'll take days/weeks/months; first you must ___; you'll need help from ___; it'll cost a lot of money; the best you can do is a lesser, unreliable version; " +
                "you and your allies risk danger from ___; you'll have to disenchant ___ to do it."),
        ],
        features:
        [
            new ClassFeature("Race Move", "Which race move do you have?",
            [
                new("Race: Human", "Choose one cleric spell. You can cast it as if it were a wizard spell."),
                new("Race: Elf", "Magic is as natural as breath to you. Detect Magic is a cantrip for you."),
            ]),
        ],
        level2To5:
        [
            new("Logical", "When you use strict deduction to analyse your surroundings, you can Discern Realities with INT instead of WIS."),
            new("Arcane Ward", "While you have at least one prepared spell of first level or higher, you have +2 armor."),
            Rolled("Counterspell", StatType.Int,
                "When you try to counter an arcane spell that would otherwise affect you, stake one of your prepared spells on the defense",
                "The spell is countered and has no effect on you.",
                "The spell is countered and you forget the spell you staked.",
                note: "Your counterspell protects you alone; other targets are affected as normal."),
            new("Quick Study", "When you see the effects of an arcane spell, ask the GM its name and effects. Take +1 when acting on the answers."),
            new("Prodigy", "Choose a spell. You prepare it as if it were one level lower."),
            new("Empowered Magic", "When you Cast a Spell, on a 10+ you can choose from the 7-9 list. If you do, also choose one: the spell's effects are maximized, or its targets are doubled."),
            new("Fount of Knowledge", "When you Spout Lore about something no one else has any clue about, take +1."),
            new("Know-It-All", "When another player's character comes to you for advice and you tell them what you think is best, they get +1 forward when following it, and you mark XP if they do."),
            new("Expanded Spellbook", "Add a new spell from any class's spell list to your spellbook."),
            new("Enchanter", "When you have time and safety with a magic item, ask the GM what it does. They answer truthfully."),
        ],
        level6To10:
        [
            new("Arcane Armor", "While you have at least one prepared spell of first level or higher, you have +4 armor.") { Replaces = "Arcane Ward" },
            new("Protective Counter", "When an ally within sight is affected by an arcane spell, you can counter it as if it affected you. Multiple allies must each be countered separately.") { Requires = "Counterspell" },
            new("Ethereal Tether", "When you have time with a willing or helpless subject, you can tether them to you: you perceive what they do and can Discern Realities about them or their surroundings at any distance. A willing subject can talk with you over it."),
            new("Spell Augmentation", "When you deal damage to a creature, you can end one of your ongoing spells and add its level to the damage dealt."),
            new("Self-Powered", "When you have time, arcane materials and a safe space, you can create your own place of power. Describe it and how you bind it; the GM tells you one kind of creature that will take an interest."),
            new("Master", "Choose one spell besides the one you picked for Prodigy. You prepare it as if it were one level lower.") { Requires = "Prodigy" },
            new("Greater Empowered Magic", "When you Cast a Spell, on a 10-11 you can choose from the 7-9 list. If you do, also choose one: maximize the spell's effects or double its targets. On a 12+, choose one of those for free.") { Replaces = "Empowered Magic" },
            new("Enchanter's Soul", "When you have time and safety with a magic item in a place of power, you can empower it so its next use is amplified (the GM says how).") { Requires = "Enchanter" },
            new("Highly Logical", "When you use strict deduction to analyse your surroundings, you can Discern Realities with INT instead of WIS. On a 12+, ask the GM any three questions, not limited by the list.") { Replaces = "Logical" },
            new("Mystical Puppet Strings", "When you use magic to control a person's actions, they have no memory of what you had them do and bear you no ill will."),
        ],
        resources:
        [
            new ClassResource(WizardLevel, "Your wizard level for spells, which follows your character level. Learn spells with /spell learn.", Start: 1)
            {
                ResetWithAll = false,
                GrowsWithLevel = true,
            },
        ]);
}
