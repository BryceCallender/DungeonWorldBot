using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Rolls;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters.Creation;

/// <summary>
/// Everything a player has picked so far during character creation.
/// </summary>
public class CharacterDraft
{
    public static readonly IReadOnlyList<int> StatArray = [8, 9, 12, 13, 15, 16];

    public static readonly IReadOnlyList<StatType> StatOrder =
        [StatType.Str, StatType.Dex, StatType.Con, StatType.Int, StatType.Wis, StatType.Cha];

    public static readonly IReadOnlyList<LabeledOption<Race>> RaceOptions =
    [
        new("Elf", Race.Elves),
        new("Dwarf", Race.Dwarves),
        new("Human", Race.Human),
        new("Orc", Race.Orc),
    ];

    public static readonly IReadOnlyList<LabeledOption<Alignment>> AlignmentOptions =
    [
        new("Lawful Good", Alignment.LawfulGood),
        new("Neutral Good", Alignment.NeutralGood),
        new("Chaotic Good", Alignment.ChaoticGood),
        new("Lawful Neutral", Alignment.LawfulNeutral),
        new("True Neutral", Alignment.TrueNeutral),
        new("Chaotic Neutral", Alignment.ChaoticNeutral),
        new("Lawful Evil", Alignment.LawfulEvil),
        new("Neutral Evil", Alignment.NeutralEvil),
        new("Chaotic Evil", Alignment.ChaoticEvil),
        new("Unaligned", Alignment.Unaligned),
    ];

    public string Name { get; set; } = string.Empty;
    public Race Race { get; set; }
    public Alignment Alignment { get; set; }
    public ClassKit Kit { get; private set; } = ClassKits.All[0];
    public Dictionary<StatType, int> Stats { get; } = new();

    /// <summary>
    /// The options picked for each of the class sheet's choices, keyed by the choice's name.
    /// </summary>
    public Dictionary<string, List<ClassMove>> FeatureChoices { get; } = new();

    /// <summary>
    /// The choices picked for each of the kit's gear questions, keyed by the question's position in the kit.
    /// </summary>
    public Dictionary<int, List<KitChoice>> GearChoices { get; } = new();

    public ClassSheet? Sheet => ClassSheetLibrary.For(Kit.Type);

    /// <summary>
    /// Whether the table counts item weight. When it doesn't, kit items are saved with no weight.
    /// </summary>
    public bool TrackWeight { get; init; } = true;

    public List<Item> Items { get; } = new();
    public int ArmorRating => Kit.BaseArmor + GearChoices.Values.SelectMany(c => c).Sum(c => c.Armor);
    public int DamageRating => GearChoices.Values.SelectMany(c => c).Sum(c => c.Damage);

    /// <summary>
    /// Switches class, clearing every choice that came from the old one.
    /// </summary>
    public void SetClass(ClassKit kit, IDiceRoller dice)
    {
        Kit = kit;
        FeatureChoices.Clear();
        ResetGear(dice);
    }

    /// <summary>
    /// Clears any chosen gear, leaving only the class's base items and armor.
    /// </summary>
    public void ResetGear(IDiceRoller dice)
    {
        GearChoices.Clear();
        RebuildItems(dice);
    }

    /// <summary>
    /// Answers one of the kit's gear questions, replacing any earlier answer to it.
    /// </summary>
    public void ChooseGear(int question, IEnumerable<KitChoice> choices, IDiceRoller dice)
    {
        GearChoices[question] = choices.ToList();
        RebuildItems(dice);
    }

    /// <summary>
    /// Gives the value to a stat, taking it back from whichever stat had it before.
    /// </summary>
    public void AssignStat(StatType stat, int value)
    {
        foreach (var holder in Stats.Where(s => s.Value == value).Select(s => s.Key).ToList())
            Stats.Remove(holder);

        Stats[stat] = value;
    }

    /// <summary>
    /// The highest value in the stat array that no stat has yet, or null once all six are assigned.
    /// </summary>
    public int? NextStatValue() =>
        StatArray.Except(Stats.Values).OrderDescending().Cast<int?>().FirstOrDefault();

    public bool HasAllStats => StatOrder.All(Stats.ContainsKey);

    /// <summary>
    /// How many options the player takes for a class choice. Earlier picks can allow more, like a Survivor's God
    /// cataclysm granting an extra Eternal Mark.
    /// </summary>
    public int AllowedPicks(ClassFeature feature)
    {
        var chosen = StartingMoves()
            .Where(m => !feature.Options.Contains(m))
            .Select(m => m.Name)
            .ToList();
        return Math.Min(feature.AllowedPicks(chosen), PickableOptions(feature).Count);
    }

    /// <summary>
    /// A class choice's options a new character can take. Ones that need another move first (like ship upgrades from
    /// Hell and Thunder) aren't offered yet.
    /// </summary>
    public static IReadOnlyList<ClassMove> PickableOptions(ClassFeature feature) =>
        feature.Options.Where(o => o.Requires is null).ToList();

    /// <summary>
    /// The class sheet's starting moves plus whatever the player chose for each class choice.
    /// </summary>
    public IEnumerable<ClassMove> StartingMoves() =>
        Sheet is null ? [] : Sheet.GrantedMoves.Concat(FeatureChoices.Values.SelectMany(c => c));

    public Character Build(Snowflake userId)
    {
        var characterClass = new Class { Type = Kit.Type };
        var startingMoves = StartingMoves().ToList();
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
            ArmorRating = ArmorRating + startingMoves.Sum(m => m.Armor),
            DamageModifier = startingMoves.Select(m => m.DamageDie).LastOrDefault(d => d is not null),
            DamageRating = DamageRating + startingMoves.Sum(m => m.Damage),
            Stats = StatOrder
                .Where(Stats.ContainsKey)
                .Select(type => new Stat { StatType = type, Value = Stats[type] })
                .ToList(),
            Health = new Health { MaxHP = maxHp, CurrentHP = maxHp },
            Inventory = new Inventory { Items = Items.ToList() },
            Abilities = startingMoves.Select(m => new Ability
            {
                Name = m.Name,
                Description = m.Summary,
                MaxUses = m.Uses,
                Uses = m.Uses ?? 0,
                LevelTaken = 1,
            }).ToList(),
        };
    }

    /// <summary>
    /// Builds the inventory from scratch from the kit and the gear chosen so far. Items that roll dice (coins,
    /// trophies) roll again.
    /// </summary>
    private void RebuildItems(IDiceRoller dice)
    {
        Items.Clear();

        var grants = Kit.BaseItems.Concat(GearChoices.OrderBy(c => c.Key).SelectMany(c => c.Value).SelectMany(c => c.Items));
        foreach (var grant in grants)
        {
            AddItem(grant.Create(dice));
        }
    }

    /// <summary>
    /// Adds an item, stacking it onto an existing item of the same name (e.g. extra rations).
    /// </summary>
    private void AddItem(Item item)
    {
        if (!TrackWeight)
            item.Weight = 0;

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
}

/// <summary>
/// One option in a creation menu: what the player sees, and what it stands for.
/// </summary>
public record LabeledOption<T>(string Label, T Value);
