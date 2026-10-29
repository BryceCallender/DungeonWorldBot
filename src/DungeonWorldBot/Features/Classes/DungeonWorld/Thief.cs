using DungeonWorldBot.Data.Entities;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.DungeonWorld;

/// <summary>
/// Summarized from the Dungeon World SRD by Sage LaTorra and Adam Koebel (CC BY 3.0).
/// </summary>
public static class Thief
{
    public static readonly ClassSheet Sheet = new(
        ClassType.Thief,
        "Dungeon World",
        new ClassColor("#3d4a45", "#8fa89e"),
        startingMoves:
        [
            Rolled("Tricks of the Trade", StatType.Dex,
                "When you pick locks or pockets or disable traps",
                "You do it, no problem.",
                "You still do it, but the GM offers you two options between suspicion, danger or cost."),
            Rolled("Backstab", StatType.Dex,
                "When you attack a surprised or defenseless enemy with a melee weapon, you can deal your damage or roll",
                "Choose two:",
                "Choose one:",
                options:
                [
                    "You don't get into melee with them",
                    "You deal your damage+1d6",
                    "You create an advantage: +1 forward to you or an ally acting on it",
                    "Reduce their armor by 1 until they repair it",
                ]),
            new("Flexible Morals", "When someone tries to detect your alignment, you can tell them any alignment you like."),
            new("Poisoner", "You've mastered one poison: it's no longer dangerous for you to use. You start with three uses of it, and whenever you have time to gather materials and a safe place to brew, you make three more uses for free. Applied poisons must be carefully applied to the target or what they eat or drink; touch poisons just need to touch them, even on a blade."),
            Rolled("Trap Expert", StatType.Dex,
                "When you spend a moment to survey a dangerous area",
                "Hold 3.",
                "Hold 1.",
                note: "Spend hold as you walk through the area to ask:\n• Is there a trap here, and if so, what activates it?\n• What does the trap do when activated?\n• What else is hidden here?"),
        ],
        features:
        [
            new ClassFeature("Race Move", "Which race move do you have?",
            [
                new("Race: Human", "You're a professional. When you Spout Lore or Discern Realities about criminal activities, take +1."),
                new("Race: Halfling", "When you attack with a ranged weapon, deal +2 damage."),
            ]),
            new ClassFeature("Poison", "Which poison have you mastered?",
            [
                new("Poison: Oil of Tagit", "Applied. The target falls into a light sleep."),
                new("Poison: Bloodweed", "Touch. The target deals -1d4 damage ongoing until cured."),
                new("Poison: Goldenroot", "Applied. The target treats the next creature they see as a trusted ally, until proved otherwise."),
                new("Poison: Serpent's Tears", "Touch. Anyone dealing damage to the target rolls twice and takes the better result."),
            ]),
        ],
        level2To5:
        [
            new("Poison Master", "After you've used a poison once, it's no longer dangerous for you to use."),
            new("Envenom", "You can apply even complex poisons with a pinprick. When you apply a poison that's not dangerous for you to your weapon, it's touch instead of applied."),
            new("Brewer", "When you have time to gather materials and a safe place to brew, you can create three doses of any one poison you've used before."),
            Rolled("Connections", StatType.Cha,
                "When you put out word to the criminal underbelly about something you want or need",
                "Someone has it, just for you.",
                "You'll have to settle for something close, or it comes with strings attached (your call)."),
            new("Cheap Shot", "When using a precise or hand weapon, your Backstab deals an extra +1d6 damage."),
            new("Cautious", "When you use Trap Expert, you always get +1 hold, even on a 6-."),
            new("Wealth and Taste", "When you make a show of flashing your most valuable possession, choose someone present. They'll do anything they can to get your item or one like it."),
            new("Shoot First", "You're never caught by surprise. When an enemy would get the drop on you, you act first instead."),
            new("Underdog", "When you're outnumbered, you have +1 armor."),
        ],
        level6To10:
        [
            new("Alchemist",
                "When you have time to gather materials and a safe place to brew, you can create three doses of any poison you've used before. Or describe a poison you'd like to create: " +
                "the GM says you can, with one or more caveats (only works in specific circumstances, a weaker version, slow to take effect, obvious side effects).") { Replaces = "Brewer" },
            Rolled("Escape Route", StatType.Dex,
                "When you're in too deep and need a way out, name your escape route",
                "You're gone.",
                "You can stay or go, but going costs you: leave something behind or take something with you (the GM says what)."),
            new("Disguise", "When you have time and materials, you can create a disguise that fools anyone into thinking you're another creature of about the same size and shape. Your actions can give you away, but your appearance won't."),
            new("Strong Arm, True Aim", "You can throw any melee weapon, using it to Volley. A thrown melee weapon is gone; you can never choose to reduce ammo on a 7-9."),
            new("Dirty Fighter", "When using a precise or hand weapon, your Backstab deals an extra +1d8 damage and all other attacks deal +1d4 damage.") { Replaces = "Cheap Shot" },
            new("Extremely Cautious", "When you use Trap Expert, you always get +1 hold, even on a 6-. On a 12+ you get 3 hold, and the next time you come near a trap the GM tells you what it does, what triggers it, who set it, and how to use it to your advantage.") { Replaces = "Cautious" },
            new("Serious Underdog", "You have +1 armor. When you're outnumbered, you have +2 armor instead.") { Replaces = "Underdog" },
            new("Evasion", "When you Defy Danger on a 12+, you transcend the danger: you do what you set out to, and the GM offers you a better outcome, true beauty, or a moment of grace."),
            new("Heist",
                "When you take time to plan to steal something, name it and ask the GM these questions. You and your allies take +1 forward acting on the answers:\n" +
                "• Who will notice it's missing?\n• What's its most powerful defense?\n• Who will come after it?\n• Who else wants it?"),
        ]);
}
