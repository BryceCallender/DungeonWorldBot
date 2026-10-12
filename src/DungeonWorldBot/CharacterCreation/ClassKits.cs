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

public sealed record KitQuestion(string Prompt, IReadOnlyList<KitChoice> Choices);

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
    ];

    private static ItemGrant HexedJars(string corpse) =>
        new(_ => new HexedJars
        {
            Name = "Hexed Canopic Jars",
            Amount = 3,
            Weight = 1,
            Contents = [corpse, "Empty", "Empty"],
        });

    private static int Roll(IDiceRoller dice, string expression) => dice.Roll(expression).Value;
}
