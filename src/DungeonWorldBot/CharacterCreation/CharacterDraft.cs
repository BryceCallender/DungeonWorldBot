using DiceNotation;
using DungeonWorldBot.Data.Entities;
using Remora.Rest.Core;

namespace DungeonWorldBot.CharacterCreation;

/// <summary>
/// Everything a player has picked so far during character creation.
/// </summary>
public sealed class CharacterDraft
{
    public static readonly IReadOnlyList<int> StatArray = [8, 9, 12, 13, 15, 16];

    public static readonly IReadOnlyList<StatType> StatOrder =
        [StatType.Str, StatType.Dex, StatType.Con, StatType.Int, StatType.Wis, StatType.Cha];

    public static readonly IReadOnlyList<(string Label, Race Value)> RaceOptions =
    [
        ("Elf", Race.Elves),
        ("Dwarf", Race.Dwarves),
        ("Human", Race.Human),
        ("Orc", Race.Orc),
    ];

    public static readonly IReadOnlyList<(string Label, Alignment Value)> AlignmentOptions =
    [
        ("Lawful Good", Alignment.LawfulGood),
        ("Neutral Good", Alignment.NeutralGood),
        ("Chaotic Good", Alignment.ChaoticGood),
        ("Lawful Neutral", Alignment.LawfulNeutral),
        ("True Neutral", Alignment.TrueNeutral),
        ("Chaotic Neutral", Alignment.ChaoticNeutral),
        ("Lawful Evil", Alignment.LawfulEvil),
        ("Neutral Evil", Alignment.NeutralEvil),
        ("Chaotic Evil", Alignment.ChaoticEvil),
        ("Unaligned", Alignment.Unaligned),
    ];

    public string Name { get; set; } = string.Empty;
    public Race Race { get; set; }
    public Alignment Alignment { get; set; }
    public ClassKit Kit { get; private set; } = ClassKits.All[0];
    public Dictionary<StatType, int> Stats { get; } = new();

    public List<Item> Items { get; } = new();
    public int ArmorRating { get; private set; }
    public int DamageRating { get; private set; }

    /// <summary>
    /// Switches class, resetting gear to that class's base items.
    /// </summary>
    public void SetClass(ClassKit kit, IDiceParser dice)
    {
        Kit = kit;
        ResetGear(dice);
    }

    /// <summary>
    /// Clears any chosen gear, leaving only the class's base items and armor.
    /// </summary>
    public void ResetGear(IDiceParser dice)
    {
        Items.Clear();
        ArmorRating = Kit.BaseArmor;
        DamageRating = 0;

        foreach (var grant in Kit.BaseItems)
        {
            AddItem(grant.Create(dice));
        }
    }

    public void ApplyChoice(KitChoice choice, IDiceParser dice)
    {
        ArmorRating += choice.Armor;
        DamageRating += choice.Damage;

        foreach (var grant in choice.Items)
        {
            AddItem(grant.Create(dice));
        }
    }

    /// <summary>
    /// Adds an item, stacking it onto an existing item of the same name (e.g. extra rations).
    /// </summary>
    private void AddItem(Item item)
    {
        var existing = item is HexedJars
            ? null
            : Items.FirstOrDefault(i => i is not HexedJars && i.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            Items.Add(item);
        }
        else
        {
            existing.Amount += item.Amount;
        }
    }

    public Character Build(Snowflake userId)
    {
        var characterClass = new Class { Type = Kit.Type };
        var maxHp = Stats.GetValueOrDefault(StatType.Con) + characterClass.HealthModifier;

        return new Character
        {
            ID = userId,
            Name = Name,
            Level = 1,
            Status = Status.Alive,
            Race = Race,
            Alignment = Alignment,
            Class = characterClass,
            ArmorRating = ArmorRating,
            DamageRating = DamageRating,
            Stats = StatOrder
                .Where(Stats.ContainsKey)
                .Select(type => new Stat { StatType = type, Value = Stats[type] })
                .ToList(),
            Health = new Health { MaxHP = maxHp, CurrentHP = maxHp },
            Inventory = new Inventory { Items = Items.ToList() },
        };
    }
}
