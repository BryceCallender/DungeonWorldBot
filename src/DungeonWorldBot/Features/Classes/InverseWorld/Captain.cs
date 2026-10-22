using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.InverseWorld;

public static class Captain
{
    private const string HellAndThunder = "Hell and Thunder";
    private const string LegendaryShip = "Legendary Ship";

    private static ClassMove Enhancement(string name, string summary, string? requires = null) =>
        new($"Ship: {name}", summary) { Requires = requires };

    public static readonly ClassSheet Sheet = new(
        ClassType.Captain,
        "Inverse World",
        new ClassColor("#1d3f73", "#5f86c9"),
        startingMoves:
        [
            new("Sail the Skies",
                "You have an airship and a capable crew that can sail anywhere. The ship has Control +0, 3 Stress, and cannons (2d6 damage, far, near vs. other ships). " +
                "A single hit of 10+ damage marks 1 Stress; smaller hits are ignored. With every Stress marked it can't fly. Repairs take downtime and 50 coin per Stress.\n" +
                "Anyone rolling with the ship rolls +Control instead of their stat (+1 if their WIS is 16+), and cannons deal the cannons' damage."),
            new("Captain's Share",
                "Spend downtime meeting merchants and nobles in a populated area to gain 1 Trade. Spend 1 Trade anywhere populated except where you got it:\n" +
                "• Carouse, taking +CHA to the roll\n• Fully supply the ship: unmark all Stress, gain 3 cannon ammo and months of crew rations\n" +
                "• Sell it: you're offered 3d6×10 coin; turn it down and you won't get a better offer in this area"),
            Rolled("Chart the Course", StatType.Wis,
                "When you plan a route",
                "Hold 3 Map.",
                "Hold 2 Map.",
                miss: "Hold 1 Map, but the GM adds a complication when you spend it.",
                options:
                [
                    "Reveal a shortcut or detour",
                    "Point out a safe spot to rest, hide in, or travel through",
                    "Spout Lore about an expected hazard as if you rolled a 10+",
                ],
                note: "Spend 1 Map at any time for one option. You lose all Map once you make it through.",
                optionsOn: OptionBands.All),
        ],
        features:
        [
            new ClassFeature("Drive", "What drives you to explore the world?",
            [
                new("Drive: Responsibility and Respect", "Mark XP when you put yourself at risk to protect your ship or crew."),
                new("Drive: Money and Fortune", "Mark XP when you endanger yourself or your ship for riches."),
                new("Drive: A Life of Adventure", "Mark XP when you take reckless, sudden action that puts an ally in danger."),
            ]),
            new ClassFeature("Background", "What is your background as a captain?",
            [
                new("Imperial", "You have military connections in one nation. +1 to Recruit at a military outpost or base; hirelings gained there cost \"Blind Patriotism\"."),
                new("Merchant", "With someone who values money, spend 1 Trade instead of rolling Parley: you gain leverage and take the 10+ result."),
                new("Pirate", "Your ship is a feared terror of the skies. You never need to make the Outstanding Warrants move."),
            ]),
            new ClassFeature("Ship Enhancements", "Which enhancements does your ship have?",
            [
                Enhancement("Agile", "+1 Control."),
                Enhancement("Capable Crew", "When your crew helps a player, that player takes +1. The crew can help one player at a time (two while on the ship)."),
                Enhancement("Combat Ready", "Your cannons deal +1d6 damage."),
                Enhancement("Fast", "+1 Control."),
                Enhancement("Luxurious", "Captain's Share gains 1d4 Trade instead of 1."),
                Enhancement("Sturdy", "Your ship has 1 more Stress."),
                Enhancement("Tank", "Your ship has 2 more Stress, but -1 Control."),
                Enhancement("Black Iron Cannonballs", "Your cannons' base damage becomes 2d10.", HellAndThunder),
                Enhancement("Buster Salvo", "Your cannons gain the Area tag.", HellAndThunder),
                Enhancement("Dragonfire Cannons", "Your cannons gain the Elemental (Fire) tag.", HellAndThunder),
                Enhancement("Ghost Ship", "A constant dark fog surrounds your ship without hindering your navigation. Pursuers always lose track of you.", LegendaryShip),
                Enhancement("Opulent Ship", "Lavish silks, jewels and gold. Whenever you gain Trade, gain twice as much.", LegendaryShip),
                Enhancement("Storm Ship", "Your ship runs on electricity: cannons gain Elemental (Electric) and Ignores Armor.", LegendaryShip),
            ])
            {
                Picks = 2,
                ExtraPicks = [("Renovations", 2), (HellAndThunder, 1), (LegendaryShip, 1)],
            },
        ],
        level2To5:
        [
            new("Discerning Dabbler", "Take a non-multiclass move from any class, treating your level as one lower unless the move rolls +WIS or improves your senses.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true) },
            new("Going Merry", "When you Make Camp with Stress marked, your crew poorly repairs it, turning one full Stress into a half-mark (they can't clear half-marks). With any half-marks, a single hit of 6+ damage marks Stress. Fill half-marks before marking new ones."),
            new(HellAndThunder, "Unlock the Black Iron Cannonballs, Buster Salvo and Dragonfire Cannons enhancements and gain one of them."),
            new("Life of the Party", "On a 12+ Carouse, choose as many options as you like. People will talk about it for years, and you're a local celebrity."),
            new("Renovations", "Choose two more ship enhancements. Captain's Share gains the option to swap one existing enhancement for another."),
            new("Ride of the Valkyries", "When you make a big show of arriving at the last moment to save the day, you and everyone helping you take +1 forward."),
            Rolled("Swashbuckling", StatType.Wis,
                "When you improvise a daring plan on the fly",
                "Hold 3.",
                "Hold 2.",
                miss: "Hold 1, but the GM also holds 1 for an enemy to use.",
                options:
                [
                    "Leap around, over or through an obstacle in your path",
                    "Create an obstacle or distraction an enemy must deal with",
                    "Divert attention from yourself to an ally, or from an ally to yourself",
                ],
                note: "Spend 1 hold at any time for one option.",
                optionsOn: OptionBands.All),
            Rolled("Take Charge", StatType.Cha,
                "When you rein in a group of miscreants",
                "They calm down and do as you say.",
                "The GM picks: they calm down but don't do what you say, or they do it while staying agitated.",
                miss: "You have a mutiny on your hands."),
            new("The Road Less Traveled", "When you make it through a difficult route, it remembers you: on the way back, natural hazards and threats leave you and your companions alone."),
            new("The Spoils of War", "When you win a great victory over a city or wealthy person, gain 1d4-1 Trade. If that's 0, you find a treasure map; follow it to the end for 1d4 Trade worth of goods."),
            new("Treasure Hunter",
                "While pursuing a treasure, gain 1 Payoff each time you suffer a major setback or someone beats you to it. When you finally get it, spend all your Payoff, gaining one per point:\n" +
                "• 1d4 Trade\n• A really cool unique item\n• A treasure map\n• As much mundane gear as you want\n• A moment of sweet revenge or irony"),
        ],
        level6To10:
        [
            new("Big Damn Heroes", "When it's dramatically appropriate, your crew flies the ship in to rescue you from a tight spot, even manning the cannons."),
            Rolled("Captain's Word", StatType.Cha,
                "When you give an order to non-hostile NPCs",
                "They obey as best they can before they even think about it.",
                "The GM picks one:",
                options:
                [
                    "They do it, but not well or not quite how you wanted",
                    "They offer you something they think you want instead",
                    "They stop what they're doing and turn their attention to you",
                ],
                optionsOn: OptionBands.WeakHit) with { Requires = "Take Charge" },
            new("Discerning Initiate", "Take another non-multiclass move from any class, treating your level as one lower unless the move rolls +WIS or improves your senses.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true), Requires = "Discerning Dabbler" },
            new("Klabautermann", "A guardian spirit lives in your ship: an NPC with Priest +2, Protector +2, Loyalty +2 and Cost: Protect the Ship. It only obeys you, never leaves the ship, can pilot it alone, and returns at dawn if killed while the ship survives."),
            new(LegendaryShip, "Unlock the Ghost Ship, Opulent Ship and Storm Ship enhancements and gain one of them.") { Requires = $"Renovations OR {HellAndThunder}" },
            new("The Master of The Ship", "At the helm of your own ship, it has +1 Control, and you and the ship take 2 less damage from every attack (Piercing and Ignores Armor don't bypass this)."),
            new("Welcome to the Crew", "When you spend downtime somewhere populated, gain up to two hirelings with skills of your choice."),
            new("Well Supplied", "When you Make Camp on your ship, you and your allies each restock up to 3 uses of rations, bandages, adventuring gear or ammo."),
        ],
        resources:
        [
            new ClassResource("Trade", "Gained from Captain's Share. Spend it in another populated area to Carouse, resupply the ship, or sell it.", Start: 1) { ResetWithAll = false },
            new ClassResource("Map", "Held from Chart the Course and spent on shortcuts, safe spots and lore. Lost once you make it through.", Start: 1) { ResetTo = 0 },
            new ClassResource("Ship Stress", "Marked when the ship takes 10+ damage in one hit. With every box marked, it can't fly.", Max: 3)
            {
                ResetWithAll = false,
                Bonuses = [new("Ship: Sturdy", Max: 1), new("Ship: Tank", Max: 2)],
            },
            new ClassResource("Cannon Ammo", "Spent when the cannons fire.", Start: 5) { ResetWithAll = false },
            new ClassResource("Payoff", "Gained from setbacks while pursuing a treasure, all spent when you get it.", GrantedBy: "Treasure Hunter"),
        ]);
}
