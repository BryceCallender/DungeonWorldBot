using DungeonWorldBot.Data.Entities;
using static DungeonWorldBot.ClassSheets.ClassMove;

namespace DungeonWorldBot.ClassSheets.InverseWorld;

public static class Collector
{
    private const string Lore = "Lore";

    public static readonly ClassSheet Sheet = new(
        ClassType.Collector,
        "Inverse World",
        startingMoves:
        [
            new("Keeper of Curios",
                "You keep a themed collection of rare curiosities (5 weight). When you take a few moments to dig through it, say what you want and what it should do. " +
                "You could have anything, but the GM tells you one to four catches, like:\n" +
                "• It only works once\n• It wasn't meant for this\n• It takes a lot of time and effort to use\n• It won't work unless you ___\n" +
                "• Its effects are incredibly specific\n• It's close to what you wanted, but not quite\n• You'll need ___'s help to use it safely"),
            Rolled("Curiosity", null,
                "When you put yourself at risk to check something out",
                "Ask the GM one question about the risks, and they answer it as clearly as they can.",
                "Ask the GM one question about the risks, and they tell you what more you'd need to do to find out.",
                rollsWith: Lore),
            new("Make It Count",
                "When you use the last use of a piece of gear, it gets +1 to all its numbers and rolls to use it. You can destroy gear without uses (a weapon, a shield) while using it for the same +1. " +
                "E.g. the last arrow gives +1 Volley and +1 damage; a sacrificed shield gives +1 armor against that attack."),
            new("Wealth and Taste", "When you flash a valuable possession around, choose an NPC present: they'll do anything they can to get it, or one like it."),
        ],
        features:
        [
            new ClassFeature("Drive", "What drives you to expand your collection?",
            [
                new("Drive: Holding the Key", "Mark XP when you keep dangerous things away from those who would abuse them."),
                new("Drive: Money and Fortune", "Mark XP when you endanger yourself or your friends for riches."),
                new("Drive: Show Off", "Mark XP when you impress someone with your wealth or gear."),
            ]),
            new ClassFeature("Predilection", "What is the nature of your collection? (It also sets your Lore stat)",
            [
                new("Predilection: Living", "**Lore is WIS.** Your curios are living creatures. Ones you pull out with Keeper of Curios can act remotely with minds of their own; order them to act alone by Defying Danger with +WIS.") { SetsClassStat = StatType.Wis },
                new("Predilection: Magical", "**Lore is CHA.** Your curios are mystical. You sense any magical effect close by: its rough direction, distance and nature.") { SetsClassStat = StatType.Cha },
                new("Predilection: Mundane", "**Lore is INT.** Your curios are practical. Choose a resource (adventuring gear, weapons, ammo, bandages or rations); spend a minute in your collection to restock 1 use of it.") { SetsClassStat = StatType.Int },
            ])
            {
                ExtraPicks = [("Expanded Collection", 1), ("Complete Collection", 1)],
            },
        ],
        level2To5:
        [
            new("Always Bring a Spare", "New limited-use equipment you buy or find comes with one extra use. Single-use things from Keeper of Curios get two uses."),
            new("Expanded Collection", "Choose a second Predilection and gain its move (but not its Lore stat)."),
            new("Happy Salesman", "When you add trinkets and curios to your leverage, Parley with +Lore instead of CHA. You can always ask for curios as extra payment for your services."),
            new("Healthy Competition", "You have a rival you can't quite trust, never far behind you. Turn to them for aid and they'll help, but you'll owe them a favor they choose when to collect."),
            new("Identify", "Spend time safely testing something and the GM tells you what it does and how to use it."),
            new("Just What I Needed", "When you loot for supplies, you always find 1 use of whatever basic equipment you need, if it's remotely plausible."),
            new("Life of the Party", "On a 12+ Carouse, choose as many options as you like. You become a local celebrity whose name carries weight here."),
            new("Lucky Charm", "When you Make Camp, set your Luck to 3. On a 6-, spend 1 Luck to reroll; if it succeeds, explain how it was pure luck. At 0 Luck, take -1 ongoing until you get more."),
            new("Obsessive Dabbler", "Take a non-multiclass move from any class, treating your level as one lower unless it relates to your Predilections.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true) },
            new("Treasure Hunter",
                "While pursuing a treasure, gain 1 Payoff each time you suffer a major setback or someone beats you to it. When you get it, spend all your Payoff, gaining one per point:\n" +
                "• A treasure map\n• As much mundane gear as you want\n• A really cool unique item\n• A moment of sweet revenge or irony"),
            new("World of Pure Imagination", "Spend a whole day setting up and you can Carouse without spending money. When you Carouse, take +Lore."),
        ],
        level6To10:
        [
            new("Complete Collection", "Choose your last Predilection and gain its move (but not its Lore stat).") { Requires = "Expanded Collection" },
            new("Connoisseur", "When you decide something you just found is especially valuable, say why. Add or remove one of its tags, or give it a unique function."),
            new("Healthy Friendship", "You and your rival make peace. Gain 1 Rivalry now and whenever you help them. When you're in trouble, spend 1 Rivalry and they show up just in time to save the day (and steal the show) or tip the odds your way.") { Requires = "Healthy Competition" },
            new("Mental Fortitude", "On a 12+ Defy Danger with Lore, you succeed beyond expectations: the GM offers a better outcome, a stroke of genius, or a golden opportunity."),
            new("Obsessive Initiate", "Take another non-multiclass move from any class, treating your level as one lower unless it relates to your Predilections.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true), Requires = "Obsessive Dabbler" },
            new("Quality Goods", "After the GM gives the catches on something from Keeper of Curios, you can veto one."),
            Rolled("Speaker of Curios", null,
                "When you command something that fits your collection's theme to take a specific action",
                "It obeys as well as it can, even if it normally couldn't do that.",
                "It does it, but badly, unexpectedly, or with consequences (the GM says what).",
                rollsWith: Lore),
            new("Supremely Lucky", "Making Camp sets your Luck to 4 instead of 3. When you'd take damage, spend 1 Luck to prevent it and describe the comedic, contrived or miraculous reason why.") { Requires = "Lucky Charm" },
        ],
        resources:
        [
            new ClassResource("Luck", "Set to 3 when you Make Camp. Spend 1 to reroll a 6-. At 0, take -1 ongoing.", Start: 3, GrantedBy: "Lucky Charm")
            {
                Bonuses = [new("Supremely Lucky", Start: 1)],
            },
            new ClassResource("Payoff", "Gained from setbacks while pursuing a treasure, all spent when you get it.", GrantedBy: "Treasure Hunter"),
            new ClassResource("Rivalry", "Gained when you help your rival. Spend 1 for them to show up just in time.", Start: 1, GrantedBy: "Healthy Friendship") { ResetWithAll = false },
        ],
        classStat: new ClassStat(Lore, "Predilection"));
}
