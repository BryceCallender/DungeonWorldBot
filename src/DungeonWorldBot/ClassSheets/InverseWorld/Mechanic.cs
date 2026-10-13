using DungeonWorldBot.Data.Entities;
using static DungeonWorldBot.ClassSheets.ClassMove;

namespace DungeonWorldBot.ClassSheets.InverseWorld;

public static class Mechanic
{
    private const string ResearchAndDevelopment = "Research and Development";

    private static ClassMove Weapon(string name, string summary, string? requires = null) =>
        new($"Primary Weapon: {name}", summary) { Requires = requires };

    private static ClassMove Movement(string name, string summary, string? requires = null) =>
        new($"Movement: {name}", summary) { Requires = requires };

    private static ClassMove System(string name, string summary, string? requires = null) =>
        new($"Advanced System: {name}", summary) { Requires = requires };

    public static readonly ClassSheet Sheet = new(
        ClassType.Mechanic,
        "Inverse World",
        startingMoves:
        [
            new("Mechanical Suit",
                "You have a unique mech suit only you can operate. Describe it. While wearing it you have 1 armor and can use the suit as a close weapon. " +
                "Hack and Slash in your suit rolls +INT instead of STR. Pick one Primary Weapon, Movement Method and Advanced System for it."),
            new("Hold Together",
                "When you'd take damage in your suit, mark Stress on it to negate the damage. Each Stress marks one:\n" +
                "• **Damaged:** Movement Methods are disabled (it can still walk)\n• **Disabled:** Advanced Systems stop working\n" +
                "• **Disarmed:** Primary Weapons are disabled (the suit is still a close weapon)\n" +
                "Unmark one when you Make Camp. With every box marked, the suit is destroyed and takes a week to rebuild."),
            new("Let Me See That",
                "When you take a few moments to examine something interesting, ask the GM two of these, and they answer truthfully:\n" +
                "• What does this do?\n• Who made this?\n• What's wrong with this, and how might I fix it?\n• What has been done most recently with this, or to this?"),
        ],
        features:
        [
            new ClassFeature("Drive", "What drives you to build machinery?",
            [
                new("Drive: Invention and Ingenuity", "Mark XP when you use a device or machine in a way it wasn't meant to be used."),
                new("Drive: To Save the Day", "Mark XP when you dive into danger to protect someone or something important."),
                new("Drive: Unfettered Mayhem", "Mark XP when you cause great collateral damage and don't even care."),
            ]),
            new ClassFeature("Specialization", "What is your area of expertise?",
            [
                new("Engineering and Mechanics", "Your suit has redundant systems. Hold Together gains a fourth box: **Dinged**, which has no penalty."),
                new("Physics and Probability", "Your suit exploits Inverse physics. It also has the **Cloudwalker** Movement Method: it can fly through clouds and rain."),
                new(ResearchAndDevelopment, "Your suit is packed with tech. Choose one extra suit option from any one list."),
                new("Theory and Analysis", "Let Me See That asks three questions instead of two."),
            ]),
            new ClassFeature("Primary Weapon", "What is your suit's Primary Weapon?",
            [
                Weapon("Artillery Strike", "Far, area, indirect, 1 ammo. Restock 1 ammo when you Make Camp."),
                Weapon("Full Arsenal", "Close. Each attack, add one tag: hand, reach, +1 damage, forceful, messy or stun."),
                Weapon("Energy Cannon", "Near, piercing 2, 2 ammo. Restock 2 ammo when you Make Camp."),
                Weapon("Rocket Fist", "Reach, near, forceful. It can grab things, and returns after use."),
                Weapon("Flamethrower", "Reach, ignores armor, Elemental (Fire).", "War Machine"),
                Weapon("Heat-seeking Missile", "Near, far, 1 ammo, +1 to Volley with it. Restock 1 ammo when you Make Camp.", "War Machine"),
            ])
            {
                ExtraPicks = [("War Machine", 1), (ResearchAndDevelopment, 1)],
            },
            new ClassFeature("Movement Method", "What is your suit's Movement Method?",
            [
                Movement("Arachnoid", "Move along walls and ceilings as easily as floors."),
                Movement("Glider", "You can't fly, but you can fall any distance safely and steer where you land."),
                Movement("Rockets", "Run along the ground at incredible speeds and make long horizontal leaps."),
                Movement("Jumper", "Jump incredible heights and take no damage from controlled falls."),
                Movement("Giant Drill", "Close, messy. Burrow through rock and stone.", "Mobile Suit"),
                Movement("Amphibious", "Breathe and swim perfectly underwater.", "Mobile Suit"),
                Movement("Zero G", "Breathe and fly perfectly in space.", "Mobile Suit"),
            ])
            {
                ExtraPicks = [("Mobile Suit", 1), (ResearchAndDevelopment, 1)],
            },
            new ClassFeature("Advanced System", "What is your suit's Advanced System?",
            [
                System("Aiming Systems", "Volley in your suit with +INT instead of DEX."),
                System("Covered Cockpit", "You have 2 armor in your suit instead of 1."),
                System("Sensors", "Discern Realities in your suit asks one extra question, even on a 6-."),
                System("Shields", "Defend in your suit gains 1 extra hold, even on a 6-."),
                System("Arc-Current Field", "Your melee attacks gain Elemental (Electric).", "Battle Tech"),
                System("Fold-Space Drive", "Mark 1 Stress on your suit to teleport anywhere within near range.", "Battle Tech"),
            ])
            {
                ExtraPicks = [("Battle Tech", 1), (ResearchAndDevelopment, 1)],
            },
        ],
        level2To5:
        [
            new("Automaton", "Your suit has an AI of your own design. It operates with nobody inside and follows your orders without question."),
            new("Backup Weapon", "You've built your own magitech gun: near, piercing 2, 2 ammo. Restock 2 ammo when you Make Camp."),
            new("Battle Tech", "Unlock the Arc-Current Field and Fold-Space Drive Advanced Systems and add one to your suit."),
            new("Don't Scratch the Paint", "When you mark Stress on your suit, take +1 forward against whatever caused it."),
            Rolled("Eject Button", StatType.Int,
                "When you suddenly really don't want to be in your suit, choosing a spot within near range to escape to",
                "Choose up to three:",
                "Choose two:",
                options:
                [
                    "You're there in an instant; nothing near the suit can react",
                    "Nothing dangerous is waiting where you land",
                    "Something on the suit explodes: mark 1 Stress and deal your damage to everything within reach of it",
                    "Your ejection seat slams into an enemy on the way: deal your damage to them",
                ]),
            new("Fire Everything", "When an enemy is really starting to annoy you, your suit's weapons deal +1d6 damage against them."),
            Rolled("Jury-Rig", StatType.Int,
                "When you quickly fix, repurpose or build a device on the spot, describing what you're doing",
                "It holds together exactly as long as you need it to.",
                "Choose one:",
                options:
                [
                    "It works, but not for long, so hurry",
                    "It works, but with a weird quirk or complication",
                ]),
            new("Mechanical Dabbler", "Take a non-multiclass move from any class, treating your level as one lower unless it relates to science, technology or knowledge.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true) },
            new("Mid-Flight Repairs", "Ignore the slow tag on repair kits. When attacked while repairing something, you and your vehicle get +1 armor against it."),
            new("Mobile Suit", "Unlock the Giant Drill, Amphibious and Zero G Movement Methods and add one to your suit."),
            Rolled("Quick Repairs", StatType.Int,
                "When you spend about an hour patching up your suit or a vehicle",
                "Unmark 1 Stress.",
                "Unmark 1 Stress, but it's temporary: you'll have to mark it again later (the GM says when)."),
            new("War Machine", "Unlock the Flamethrower and Heat-seeking Missile Primary Weapons and add one to your suit."),
            Rolled("What Makes You Tick", StatType.Int,
                "When you take something apart with your toolkit",
                "Choose three:",
                "Choose two:",
                options:
                [
                    "It doesn't take long",
                    "You don't attract attention",
                    "You can put it back together without much effort",
                    "You salvage something valuable",
                ]),
        ],
        level6To10:
        [
            new("Force of Will", "When you scream to the heavens and declare victory over someone, ignore all debilities and penalties to rolls against them until one of you defeats the other. Once per target."),
            new("Rescue Ranger", "Your suit has a tractor beam, grappling hook or similar. Use it to pull a person or unattended object within near range to your suit instantly."),
            new("Strength of a Thousand", "Your suit's melee attacks gain forceful and it can lift anything. In your suit, What Makes You Tick works on buildings, walls and other huge things."),
            new("The Big Red Button",
                "Slam the Big Red Button and it breaks; choose one:\n" +
                "• The suit explodes: everything within reach takes your damage\n" +
                "• A missile salvo: Volley with area, messy, forceful, near and far, rolling +INT instead of DEX\n" +
                "• Experimental escape: teleport to a spot within near range, optionally leaving a smokescreen\n" +
                "Repair it when you Make Camp.") { Uses = 1 },
            new("Transformer", "During downtime in a workshop, swap one Primary Weapon, Movement Method or Advanced System for another (/ability remove the old one, then /class learn the new one)."),
            new("What Did I JUST Tell You", "When you mark Stress on your suit, take +1 ongoing against whatever caused it until it's repaired.") { Replaces = "Don't Scratch the Paint" },
        ],
        resources:
        [
            new ClassResource("Suit Stress", "Marked to negate damage while in your suit. Unmark one when you Make Camp. With every box marked, the suit is destroyed.", Max: 3)
            {
                ResetWithAll = false,
                Bonuses = [new("Engineering and Mechanics", Max: 1)],
            },
        ]);
}
