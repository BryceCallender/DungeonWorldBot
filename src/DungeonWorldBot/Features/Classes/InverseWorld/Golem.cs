using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.InverseWorld;

public static class Golem
{
    private const string Force = "Force";
    private const string WeCanRebuildHim = "We Can Rebuild Him";
    private const string WeHaveTheTechnology = "We Have the Technology";

    private static ClassMove BodyOption(string name, string summary, int armor = 0, string? requires = null) =>
        new($"Built for Battle: {name}", summary) { Armor = armor, Requires = requires };

    public static readonly ClassSheet Sheet = new(
        ClassType.Golem,
        "Inverse World",
        new ClassColor("#66625b", "#aca69c"),
        startingMoves:
        [
            new("Built for Battle", "You were built to fight: your body is a weapon with the hand and close tags. Choose two Built for Battle options."),
            new("Eternal Sentinel", "You don't eat, drink or sleep; ignore moves that tell you to mark rations. Healing potions, bandages and poultices don't work on you, but other healing does, and 1 use of a repair kit heals you 4 HP."),
            new("Prime Directive", "Take +1 forward to carry out an order from someone you have a bond with, and -1 to actions that ignore, resist or contradict one. To refuse such an order outright before it's done, erase one of your bonds with that player."),
        ],
        features:
        [
            new ClassFeature("Drive", "What drives you to fulfill your duties?",
            [
                new("Drive: Recognition", "Mark XP when you make someone acknowledge the effort you've put in."),
                new("Drive: Fulfill the Directive", "Mark XP when you successfully advance the agenda of those who control you."),
                new("Drive: Quest for Identity", "Mark XP when you do what you believe is right, whatever your orders."),
            ]),
            new ClassFeature("Composition", "What are you made of? (It also sets your Force stat)",
            [
                new("Composition: Solid", "**Force is STR.** You're made of something like iron, stone or crystal: 2 armor.") { SetsClassStat = StatType.Str, Armor = 2 },
                new("Composition: Putty", "**Force is CON.** You're made of something like clay, taffy or flesh. When you roll a 10+ on anything, heal 2 HP. Choose one damage source (fire, acid, cold iron, magic, poison) you can't naturally heal from.") { SetsClassStat = StatType.Con },
                new("Composition: Liquid", "**Force is DEX.** You're made of something like water, cloud or fabric, and can squeeze through gaps of any size.") { SetsClassStat = StatType.Dex },
                new("Composition: Danger", "**Force is DEX.** You're made of something like fire, acid or poison. Anyone touching you for more than a moment without protection takes 1d4 damage.") { SetsClassStat = StatType.Dex },
            ])
            {
                ExtraPicks = [("Flexible Composition", 1)],
            },
            new ClassFeature("Built for Battle", "Which Built for Battle options does your body have?",
            [
                BodyOption("Armored", "Your body has +1 armor.", armor: 1),
                BodyOption("Burning", "Your body leaves lasting, burning wounds on those you damage."),
                BodyOption("Caustic", "Your body has piercing 2."),
                BodyOption("Detachable", "Your body has the near tag and needs no ammo. When a move says to mark ammo, take 1d4 damage (ignores armor) instead."),
                BodyOption("Flexible", "Your body has the precise tag."),
                BodyOption("Violent", "Your body has the messy and forceful tags."),
                BodyOption("Explosive", "Your body gains the area tag.", requires: WeHaveTheTechnology),
            ])
            {
                Picks = 2,
                ExtraPicks = [(WeCanRebuildHim, 1), (WeHaveTheTechnology, 1)],
            },
            new ClassFeature("Force Move", "Which move do you start with? (You can take the other later as an advanced move)",
            [
                Rolled("Immovable Object", null,
                    "When an enemy tries to move you against your will",
                    "Choose two:",
                    "Choose one:",
                    options:
                    [
                        "You aren't moved",
                        "You throw, push or drag them as far as they'd have moved you, in any direction",
                    ],
                    rollsWith: Force),
                Rolled("Unstoppable Force", null,
                    "When you try to remove or plow through every obstacle in your way",
                    "Hold 3.",
                    "Hold 2.",
                    miss: "Hold 1, but the GM adds a complication when you spend it.",
                    options:
                    [
                        "Force your way past an obstacle",
                        "Hold, lift or shove an obstacle aside long enough for an ally or two to get past",
                        "Do great damage to an object or obstacle in your way",
                    ],
                    note: "Spend 1 hold at any time for an option.",
                    rollsWith: Force,
                    optionsOn: OptionBands.All),
            ])
            {
                LaterPicksAreAdvances = true,
            },
        ],
        level2To5:
        [
            new("Arcane Void", "+2 armor against magical attacks, and nearby allies get +1 armor against them."),
            new("Edible", "You're made of something edible (tasty is optional). When an ally needs to mark a ration, you can take 1 damage (ignores armor) instead."),
            Rolled("Elemental Transference", null,
                "You can move through or along anything made mostly of what you're made of. When you do",
                "You pass through without consequence or trace.",
                "Some of the material gets stuck in you, leaving signs of your passage or causing trouble (the GM says what).",
                rollsWith: Force),
            new("Emotions I Don't Understand", "When you Defend someone you have a bond with, you can roll +Force instead of CON."),
            new("Flexible Composition", "Choose a second Composition and gain its move (but not its Force stat)."),
            new("Iron Dabbler", "Take any move from the Survivor or Mechanic list. If it's Mechanical Suit, instead pick one Primary Weapon, Movement Method and Advanced System that apply to your body.") { Multiclass = MulticlassGrant.FromClasses(ClassType.Survivor, ClassType.Mechanic) },
            new("Material Girl", "When you or an ally needs ammo, weaponry or adventuring gear, take 1d4 damage (ignores armor) to rip 1 use of it out of your body."),
            new("Pinned Down",
                "When you'd deal melee damage, you can grab the target instead. You never lose your grip unless you choose to. End the grab at any time to:\n" +
                "• Slam them into something, dealing your damage\n• Pin them: neither of you can do anything\n• Throw them somewhere within reach, landing prone"),
            new("Search and Destroy", "When you're ordered to kill something, take +1d4 damage forward against it."),
            new(WeCanRebuildHim, "You have 3 Built for Battle options. After a few safe minutes altering your body, swap one for another."),
        ],
        level6To10:
        [
            new("Clash", "When you block a physical attack, you can grab the attacker as with Pinned Down, even without that move."),
            Rolled("Core Overload", null,
                "When you overload the energies that keep you alive, taking 1d8 damage (ignores armor)",
                "You explode violently, dealing your damage to everything within reach.",
                "You explode violently, dealing your damage to everything within reach, with massive collateral damage (the GM says how).",
                rollsWith: Force),
            new("Explosive Entrance", "On a 10+ Elemental Transference, you can burst out violently: take the 7-9 result instead, but deal your damage to any enemies within reach of where you exit.") { Requires = "Elemental Transference" },
            new("Exterminate", "When you're ordered to kill something, take +1d8 damage forward against it.") { Replaces = "Search and Destroy" },
            new("Magical Dabbler", "Take a non-multiclass move from any class, treating your level as one lower unless it's magic-based.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true) },
            new("Material World",
                "Take a safe moment to reshape the terrain around you and say what you want. It's always possible, but the GM tells you one to three catches:\n" +
                "• It's loud, drawing attention as you finish\n• You need ___'s help\n• It uses up gear or equipment\n• It's temporary at best\n• The area is permanently scarred"),
            new("Meteor Throw", "Throw allies anywhere within near range at no risk to them. Pinned Down gains:\n• Throw them anywhere within near or far range") { Requires = "Pinned Down OR Clash" },
            new("Ultimate Force", "On a 12+ Defy Danger with Force, you succeed beyond expectations: the GM offers a better outcome, a moment of physical perfection, or a chance for great destruction."),
            new(WeHaveTheTechnology, "You have 3 Built for Battle options, and whenever you make a move you can swap one. Unlocks the Explosive option (area tag).") { Replaces = WeCanRebuildHim },
        ],
        classStat: new ClassStat(Force, "Composition"));
}
