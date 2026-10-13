using DungeonWorldBot.Rolling;
using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.CharacterCreation;

/// <summary>
/// Creates a fresh inventory item. Most items are fixed, but some (coins, trophies) roll dice when granted.
/// </summary>
public sealed record ItemGrant(Func<IDiceRoller, Item> Create)
{
    public static ItemGrant Of(string name, int amount, int weight) =>
        new(_ => new Item { Name = name, Amount = amount, Weight = weight });
}

/// <param name="Armor">Armor added to the character when chosen.</param>
/// <param name="Damage">Damage rating added to the character when chosen.</param>
public sealed record KitChoice(string Label, IReadOnlyList<ItemGrant> Items, int Armor = 0, int Damage = 0);

/// <param name="Picks">How many different choices the player takes, e.g. "choose two".</param>
public sealed record KitQuestion(string Prompt, IReadOnlyList<KitChoice> Choices, int Picks = 1);

public sealed record ClassKit(
    ClassType Type,
    string DisplayName,
    IReadOnlyList<ItemGrant> BaseItems,
    int BaseArmor,
    IReadOnlyList<KitQuestion> Questions);

/// <summary>
/// The starting gear for every class, in the order they're offered during character creation.
/// </summary>
public static class ClassKits
{
    private static readonly ItemGrant DungeonRations = ItemGrant.Of("Dungeon Rations", 5, 1);
    private static readonly ItemGrant AdventuringGear = ItemGrant.Of("Adventuring Gear", 5, 1);

    private static readonly KitQuestion ArmorOrSupplies = new("Which set of items would you like to have?",
    [
        new("Leather Armor (+1 armor)", [ItemGrant.Of("Leather Armor", 1, 1)], Armor: 1),
        new("3 Healing Potions and Adventuring Gear (5 uses)", [ItemGrant.Of("Healing Potions", 3, 0), AdventuringGear]),
    ]);

    public static readonly IReadOnlyList<ClassKit> All =
    [
        new(ClassType.Channeler, "Channeler",
            [DungeonRations, ItemGrant.Of("Soothing Balm", 1, 0)],
            BaseArmor: 0,
            [
                ArmorOrSupplies,
                new("Which of these backup weapons would you like to have?",
                [
                    new("Knife (hand)", [ItemGrant.Of("Knife (hand)", 1, 1)]),
                    new("Bo Staff (close, Two-Handed)", [ItemGrant.Of("Bo Staff (close, Two-Handed)", 1, 1)]),
                ]),
                new("Which set of items would you like to have?",
                [
                    new("Smelling Salts and 1 Antitoxin", [ItemGrant.Of("Smelling Salts", 1, 0), ItemGrant.Of("Antitoxin", 1, 0)]),
                    new("Pouch with 1d10 coins", [new(dice => new Item { Name = "Coins", Amount = Roll(dice, "1d10"), Weight = 0 })]),
                ]),
            ]),

        new(ClassType.Necromancer, "Necromancer",
            [DungeonRations, ItemGrant.Of("Shovel", 1, 1), ItemGrant.Of("Needle and Thread", 1, 0)],
            BaseArmor: 0,
            [
                ArmorOrSupplies,
                new("Which of these weapons would you like to have?",
                [
                    new("Bone Dagger (hand)", [ItemGrant.Of("Bone Dagger (hand)", 1, 1)]),
                    new("Skull Staff (close, Two-Handed)", [ItemGrant.Of("Skull Staff (close, Two-Handed)", 1, 1)]),
                ]),
                new("Which corpse is in one of your three Hexed Canopic Jars?",
                [
                    new("A One Armed Dwarf", [HexedJars("A one-armed Dwarf")]),
                    new("Someone You Used to Know", [HexedJars("Someone you used to know")]),
                    new("A Skinless Hound", [HexedJars("A Skinless Hound")]),
                ]),
            ]),

        new(ClassType.Paladin, "Paladin",
            [DungeonRations, ItemGrant.Of("Scale Armor", 1, 1), ItemGrant.Of("Mark of Faith", 1, 0)],
            BaseArmor: 2,
            [
                new("Which of these weapons would you like to have?",
                [
                    new("Halberd (reach, +1 damage, Two-Handed)", [ItemGrant.Of("Halberd (reach, +1 damage, Two-Handed)", 1, 1)]),
                    new("Long Sword (close, +1 damage) and Shield (+1 armor)",
                        [ItemGrant.Of("Long Sword (close, +1 damage)", 1, 1), ItemGrant.Of("Shield", 1, 1)], Armor: 1),
                ]),
                new("Which set of items would you like to have?",
                [
                    new("Adventuring Gear (5 uses)", [AdventuringGear]),
                    new("5 Dungeon Rations and a Healing Potion", [DungeonRations, ItemGrant.Of("Healing Potions", 1, 0)]),
                ]),
            ]),

        new(ClassType.Ranger, "Ranger",
            [DungeonRations, ItemGrant.Of("Leather Armor", 1, 1), ItemGrant.Of("Bundle of Arrows", 3, 1)],
            BaseArmor: 1,
            [
                new("Which of these weapons would you like to have?",
                [
                    new("Hunter's Bow (near, far) and Short Sword (close)",
                        [ItemGrant.Of("Hunter's Bow (near, far)", 1, 1), ItemGrant.Of("Short Sword (close)", 1, 1)]),
                    new("Hunter's Bow (near, far) and Spear (reach)",
                        [ItemGrant.Of("Hunter's Bow (near, far)", 1, 1), ItemGrant.Of("Spear (reach)", 1, 1)]),
                ]),
                new("Which set of items would you like to have?",
                [
                    new("Adventuring Gear (5 uses) and a Bundle of 3 Arrows", [AdventuringGear, ItemGrant.Of("Bundle of Arrows", 3, 1)]),
                    new("Adventuring Gear (5 uses) and 5 Dungeon Rations", [AdventuringGear, DungeonRations]),
                ]),
            ]),

        new(ClassType.Slayer, "Slayer",
            [DungeonRations, ItemGrant.Of("Leather Armor", 1, 1), ItemGrant.Of("Slayer's Arsenal", 1, 1)],
            BaseArmor: 1,
            [
                new("Which of these weapons would you like to have?",
                [
                    new("Dual Battle Axes (close, +1 damage)", [ItemGrant.Of("Dual Battle Axes (close, +1 damage)", 1, 1)]),
                    new("Spear (close, thrown, near)", [ItemGrant.Of("Spear (close, thrown, near)", 1, 1)]),
                    new("Claymore (close, +1 damage, Two-Handed)", [ItemGrant.Of("Claymore (close, +1 damage, Two-Handed)", 1, 1)]),
                ]),
                new("Which set of items would you like to have?",
                [
                    new("3 Javelins (thrown, near)", [ItemGrant.Of("Javelins (thrown, near)", 3, 1)]),
                    new("Crossbow (near, +1 damage, reload)", [ItemGrant.Of("Crossbow (near, +1 damage, reload)", 1, 1)]),
                    new("A Trophy from a recent kill worth 4d8 coins",
                        [new(dice => new Item { Name = $"A Trophy from a recent kill worth {Roll(dice, "4d8")} coins", Amount = 1, Weight = 1 })]),
                ]),
            ]),

        new(ClassType.Druid, "Druid",
            [ItemGrant.Of("Token of Your Land", 1, 1)],
            BaseArmor: 0,
            [
                new("Which set of defensive items would you like to have?",
                [
                    new("Hide Armor (+1 armor, worn)", [ItemGrant.Of("Hide Armor", 1, 1)], Armor: 1),
                    new("Wooden Shield (+1 Armor, held)", [ItemGrant.Of("Wooden Shield", 1, 1)], Armor: 1),
                ]),
                new("Which of these weapons would you like to have?",
                [
                    new("Shillelagh (close, One-Hand)", [ItemGrant.Of("Shillelagh", 1, 2)]),
                    new("Staff (close, Two-Handed)", [ItemGrant.Of("Staff", 1, 1)]),
                    new("Spear (close, thrown)", [ItemGrant.Of("Spear", 1, 1)]),
                ]),
                new("Which set of extra items would you like to have?",
                [
                    new("Adventuring Gear (5 uses)", [AdventuringGear]),
                    new("Poultices and Herbs (2 uses, slow)", [ItemGrant.Of("Poultices & Herbs", 2, 1)]),
                    new("Halfling Pipeleaf (6 uses)", [ItemGrant.Of("Halfling Pipeleaf", 6, 0)]),
                ]),
            ]),

        new(ClassType.Battlemaster, "Battle Master",
            [DungeonRations, ItemGrant.Of("Spyglass", 1, 1), ItemGrant.Of("Dagger", 1, 1)],
            BaseArmor: 0,
            [
                new("Which set of defensive items would you like to have?",
                [
                    new("Scale Armor (+2 armor, clumsy)", [ItemGrant.Of("Scale Armor", 1, 3)], Armor: 2),
                    new("Chainmail (+1 Armor) and Adventuring Gear (5 uses)", [ItemGrant.Of("Chainmail", 1, 1), AdventuringGear], Armor: 1),
                ]),
                new("Which of these weapons would you like to have?",
                [
                    new("Warhammer (close, One-Hand) and Shield (+1 armor)",
                        [ItemGrant.Of("Warhammer", 1, 1), ItemGrant.Of("Shield", 1, 2)], Armor: 1),
                    new("Halberd (reach, +1 damage, Two-Handed)", [ItemGrant.Of("Halberd", 1, 2)], Damage: 1),
                ]),
                new("Which set of extra items would you like to have?",
                [
                    new("Bag of Books (5 uses)", [ItemGrant.Of("Bag of Books", 5, 2)]),
                    new("Bandages (3 uses, slow)", [ItemGrant.Of("Bandages", 3, 0)]),
                ]),
            ]),

        new(ClassType.Templar, "Templar",
            [DungeonRations, ItemGrant.Of("Interrogation Tools and Penance Device", 1, 1)],
            BaseArmor: 0,
            [
                new("Which of these weapons would you like to have?",
                [
                    new("Short Sword (close) and Shield (+1 armor)",
                        [ItemGrant.Of("Short Sword (close)", 1, 1), ItemGrant.Of("Shield", 1, 2)], Armor: 1),
                    new("Flail (close, +1 damage)", [ItemGrant.Of("Flail (close, +1 damage)", 1, 2)], Damage: 1),
                ]),
                new("Which defenses would you like to have?",
                [
                    new("Chainmail (+1 armor)", [ItemGrant.Of("Chainmail", 1, 1)], Armor: 1),
                    new("Shield (+1 armor)", [ItemGrant.Of("Shield", 1, 2)], Armor: 1),
                ]),
                new("Which set of items would you like to have?",
                [
                    new("Bag of Books (5 uses)", [ItemGrant.Of("Bag of Books", 5, 2)]),
                    new("A recent offering of 3d6 coins", [new(dice => new Item { Name = "Coins", Amount = Roll(dice, "3d6"), Weight = 0 })]),
                ]),
            ]),

        new(ClassType.Shaman, "Shaman",
            [DungeonRations, ItemGrant.Of("Spirit Catcher Totem", 1, 1), ItemGrant.Of("Carving Knife (hand)", 1, 1)],
            BaseArmor: 0,
            [
                new("Which of these weapons would you like to have?",
                [
                    new("Ceremonial Dagger (hand)", [ItemGrant.Of("Ceremonial Dagger (hand)", 1, 1)]),
                    new("Spear (reach, thrown, near)", [ItemGrant.Of("Spear (reach, thrown, near)", 1, 1)]),
                    new("Staff (close, Two-Handed)", [ItemGrant.Of("Staff (close, Two-Handed)", 1, 1)]),
                ]),
                new("Which defenses would you like to have?",
                [
                    new("Painted Wooden Shield (+1 armor)", [ItemGrant.Of("Painted Wooden Shield", 1, 1)], Armor: 1),
                    new("Light Hide or Bone Armor (+1 armor)", [ItemGrant.Of("Light Hide or Bone Armor", 1, 1)], Armor: 1),
                ]),
                new("Which item would you like to have?",
                [
                    new("Dreamcap Mushrooms (hallucinogenic)", [ItemGrant.Of("Dreamcap Mushrooms", 1, 0)]),
                    new("Poultices and Herbs (2 uses)", [ItemGrant.Of("Poultices & Herbs", 2, 1)]),
                    new("Antitoxin", [ItemGrant.Of("Antitoxin", 1, 0)]),
                    new("Ritual Drum", [ItemGrant.Of("Ritual Drum", 1, 1)]),
                ]),
            ]),

        new(ClassType.Captain, "Captain",
            [DungeonRations, ItemGrant.Of("Captain's Leathers", 1, 1), ItemGrant.Of("Airship", 1, 0), Coins(14)],
            BaseArmor: 1,
            [
                new("Which weapon would you like to have?",
                [
                    new("Looted Cutlass (close)", [ItemGrant.Of("Looted Cutlass (close)", 1, 1)]),
                    new("Officer's Rapier (close, precise)", [ItemGrant.Of("Officer's Rapier (close, precise)", 1, 1)]),
                    new("Wicked Hook (hand, piercing 1)", [ItemGrant.Of("Wicked Hook (hand, piercing 1)", 1, 1)]),
                ]),
                new("Which items would you like to have?",
                [
                    new("Throwing Knives (near, thrown, 3 ammo)", [ItemGrant.Of("Throwing Knives (near, thrown)", 3, 1)]),
                    new("Adventuring Gear (5 uses)", [AdventuringGear]),
                    new("Poultices and Herbs (2 uses, slow)", [ItemGrant.Of("Poultices & Herbs", 2, 0)]),
                    new("Flintlock Pistol (reach, near, +1 damage, reload) with 3 ammo",
                        [ItemGrant.Of("Flintlock Pistol (reach, near, +1 damage, reload)", 1, 1), ItemGrant.Of("Pistol Ammo", 3, 1)], Damage: 1),
                ], Picks: 2),
            ]),

        new(ClassType.Collector, "Collector",
            [ItemGrant.Of("Collection of Curios", 1, 5), DungeonRations, AdventuringGear],
            BaseArmor: 0,
            [
                new("Which items would you like to have?",
                [
                    new("A Catalogue of the Strange and Mysterious (5 uses)", [ItemGrant.Of("Catalogue of the Strange and Mysterious", 5, 1)]),
                    new("An Antique Weapon (any range) with 3 ammo", [ItemGrant.Of("Antique Weapon", 1, 1), ItemGrant.Of("Antique Weapon Ammo", 3, 1)]),
                    new("Monster Feed (8 uses) and Bandages (3 uses)", [ItemGrant.Of("Monster Feed", 8, 1), ItemGrant.Of("Bandages", 3, 0)]),
                    new("A Repair Kit (6 uses, slow) and an Appraisal Kit", [ItemGrant.Of("Repair Kit", 6, 1), ItemGrant.Of("Appraisal Kit", 1, 1)]),
                    new("40 coins and a membership card to an organization", [Coins(40), ItemGrant.Of("Membership Card", 1, 0)]),
                    new("Poultices and Herbs (2 uses) and Ratling Pipeleaf (6 uses)", [ItemGrant.Of("Poultices & Herbs", 2, 0), ItemGrant.Of("Ratling Pipeleaf", 6, 1)]),
                    new("A unique and intelligent item, pet or companion", [ItemGrant.Of("Unique Intelligent Companion", 1, 0)]),
                    new("Spare clothing for any occasion, including light armor (+1 armor)", [ItemGrant.Of("Spare Clothing and Light Armor", 1, 1)], Armor: 1),
                    new("A vehicle or mount matching your collection's theme", [ItemGrant.Of("Themed Vehicle or Mount", 1, 0)]),
                ], Picks: 4),
            ]),

        new(ClassType.Lantern, "Lantern",
            [DungeonRations, ItemGrant.Of("Lantern", 1, 1)],
            BaseArmor: 0,
            [
                new("Which defense would you like to have?",
                [
                    new("Lightweave Armor (+1 armor)", [ItemGrant.Of("Lightweave Armor", 1, 1)], Armor: 1),
                    new("Hooked Quarterstaff (close, Two-Handed), which your lantern can hang from", [ItemGrant.Of("Hooked Quarterstaff (close, Two-Handed)", 1, 1)]),
                ]),
                new("Which item would you like to have?",
                [
                    new("One Healing Potion", [ItemGrant.Of("Healing Potions", 1, 0)]),
                    new("Three Antitoxins", [ItemGrant.Of("Antitoxin", 3, 0)]),
                    new("Bag of Books (5 uses)", [ItemGrant.Of("Bag of Books", 5, 2)]),
                ]),
            ]),

        new(ClassType.Mechanic, "Mechanic",
            [DungeonRations, ItemGrant.Of("Mecha Suit", 1, 8), ItemGrant.Of("Repair Kit", 6, 1), ItemGrant.Of("Wrench (hand)", 1, 1)],
            BaseArmor: 1,
            [
                new("Which items would you like to have?",
                [
                    new("Protective Clothing (+1 armor)", [ItemGrant.Of("Protective Clothing", 1, 1)], Armor: 1),
                    new("Adventuring Gear (5 uses)", [AdventuringGear]),
                    new("Bag of Books (5 uses)", [ItemGrant.Of("Bag of Books", 5, 2)]),
                    new("Bandages (3 uses, slow)", [ItemGrant.Of("Bandages", 3, 0)]),
                    new("13 coins", [Coins(13)]),
                ], Picks: 2),
            ]),

        new(ClassType.SkyDancer, "Sky Dancer",
            [DungeonRations, ItemGrant.Of("Means of Flight", 1, 0)],
            BaseArmor: 0,
            [
                new("Which armaments would you like to have?",
                [
                    new("Weathered Shortbow (near) with a quiver of 3 ammo",
                        [ItemGrant.Of("Weathered Shortbow (near)", 1, 2), ItemGrant.Of("Arrows", 3, 1)]),
                    new("Angelic Sword (close, precise) and Light Shield (+1 armor)",
                        [ItemGrant.Of("Angelic Sword (close, precise)", 1, 1), ItemGrant.Of("Light Shield", 1, 2)], Armor: 1),
                ]),
                new("Which items would you like to have?",
                [
                    new("A flying vehicle or mount", [ItemGrant.Of("Flying Vehicle or Mount", 1, 0)]),
                    new("Bandages (3 uses, slow) and 5 Dungeon Rations", [ItemGrant.Of("Bandages", 3, 0), DungeonRations]),
                    new("Binoculars and a Compass", [ItemGrant.Of("Binoculars", 1, 0), ItemGrant.Of("Compass", 1, 0)]),
                    new("Lightweave Armor (+1 armor)", [ItemGrant.Of("Lightweave Armor", 1, 1)], Armor: 1),
                ], Picks: 2),
            ]),

        new(ClassType.Survivor, "Survivor",
            [DungeonRations, ItemGrant.Of("Memento of Your Cataclysm", 1, 0)],
            BaseArmor: 0,
            [
                new("Which heirloom would you like to have?",
                [
                    new("Your mentor's weapon (+1 damage, 3 ammo if it needs it)", [ItemGrant.Of("Mentor's Weapon (+1 damage)", 1, 2)], Damage: 1),
                    new("Your parent's armor (+1 armor)", [ItemGrant.Of("Parent's Armor", 1, 1)], Armor: 1),
                ]),
                new("Which items would you like to have?",
                [
                    new("Adventuring Gear (5 uses)", [AdventuringGear]),
                    new("Survival Knife (hand)", [ItemGrant.Of("Survival Knife (hand)", 1, 1)]),
                    new("Bandages (3 uses, slow) and 5 Dungeon Rations", [ItemGrant.Of("Bandages", 3, 0), DungeonRations]),
                    new("A mount or vehicle that's been through hard times with you", [ItemGrant.Of("Trusty Mount or Vehicle", 1, 0)]),
                ], Picks: 2),
            ]),

        new(ClassType.Walker, "Walker",
            [DungeonRations, AdventuringGear, ItemGrant.Of("Survival Knife (hand)", 1, 1)],
            BaseArmor: 0,
            [
                new("Which main weapon would you like to have?",
                [
                    new("Climbing Spear (close, reach, 1 piercing)", [ItemGrant.Of("Climbing Spear (close, reach, 1 piercing)", 1, 2)]),
                    new("War Club (close, forceful)", [ItemGrant.Of("War Club (close, forceful)", 1, 1)]),
                    new("Combat Knife (hand, close, thrown, near)", [ItemGrant.Of("Combat Knife (hand, close, thrown, near)", 1, 1)]),
                ]),
                new("Which items would you like to have?",
                [
                    new("A badge of office or symbol of high rank", [ItemGrant.Of("Badge of Office", 1, 0)]),
                    new("Leather Armor (+1 armor)", [ItemGrant.Of("Leather Armor", 1, 1)], Armor: 1),
                    new("Bandages (3 uses, slow) and 1d6 coins", [ItemGrant.Of("Bandages", 3, 0), new(dice => new Item { Name = "Coins", Amount = Roll(dice, "1d6"), Weight = 0 })]),
                    new("A Repair Kit (6 uses, slow)", [ItemGrant.Of("Repair Kit", 6, 1)]),
                    new("Throwing Knives (near, 3 ammo)", [ItemGrant.Of("Throwing Knives (near)", 3, 1)]),
                ], Picks: 2),
            ]),

        new(ClassType.Golem, "Golem",
            [],
            BaseArmor: 0,
            [
                new("You start with almost nothing. Which one thing do you have?",
                [
                    new("A heavy crushing weapon (close, +1 damage, messy)", [ItemGrant.Of("Heavy Crushing Weapon (close, +1 damage, messy)", 1, 2)], Damage: 1),
                    new("A flexible bladed weapon (close, +1 damage, precise)", [ItemGrant.Of("Flexible Bladed Weapon (close, +1 damage, precise)", 1, 2)], Damage: 1),
                    new("A long poking weapon (reach, +1 damage, forceful)", [ItemGrant.Of("Long Poking Weapon (reach, +1 damage, forceful)", 1, 2)], Damage: 1),
                    new("Armored Plates (+1 armor)", [ItemGrant.Of("Armored Plates", 1, 1)], Armor: 1),
                    new("A personal keepsake you hold dear", [ItemGrant.Of("Personal Keepsake", 1, 0)]),
                ]),
            ]),
    ];

    private static ItemGrant HexedJars(string corpse) =>
        new(_ => new HexedJars
        {
            Name = "Hexed Canopic Jars",
            Amount = 3,
            Weight = 1,
            Contents = [corpse, "Empty", "Empty"],
        });

    private static ItemGrant Coins(int amount) => ItemGrant.Of("Coins", amount, 0);

    private static int Roll(IDiceRoller dice, string expression) => dice.Roll(expression).Value;
}
