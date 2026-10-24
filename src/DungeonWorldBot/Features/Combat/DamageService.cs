using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Gear;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using d20Tek.DiceNotation.Results;

namespace DungeonWorldBot.Features.Combat;

[Scoped<IDamageService>]
public class DamageService : IDamageService
{
    public const string ExtraOption = "extra-damage";
    public const string ExtraDescription = "Extra damage the bot isn't tracking, added to the roll (e.g. 2, or -1)";
    public const string ExtraSource = "Extra";

    private readonly ICharacterService _characterService;
    private readonly IDiceRoller _diceRoller;
    private readonly IGearService _gearService;
    private readonly IArmorService _armorService;

    public DamageService(ICharacterService characterService, IDiceRoller diceRoller, IGearService gearService, IArmorService armorService)
    {
        _characterService = characterService;
        _diceRoller = diceRoller;
        _gearService = gearService;
        _armorService = armorService;
    }

    public async Task<DamageRoll?> RollAsync(Character character, string move, int extra = 0)
    {
        var bonuses = await DamageBonusesAsync(character, extra);
        var baseDice = character.DamageModifier ?? character.Class.Damage;
        var tactic = TacticRules.ExtraDamage(character);
        var expression = DiceHelper.AddModifier(tactic is not null ? $"{baseDice}+{tactic}" : baseDice, bonuses.Sum(b => b.Amount));
        if (!_diceRoller.TryRoll(expression, out var dice))
            return null;

        var gear = await ApplyGearAsync(character, move, DamageDice(dice));
        if (tactic is not null)
            gear = gear with { Notes = gear.Notes.Prepend($"{TacticRules.Current(character)!.Name} Tactics: +{tactic} damage").ToList() };
        return new DamageRoll(expression, dice, dice.Value + gear.Bonus, bonuses, gear);
    }

    public Task<GearDamage> ApplyGearAsync(Character character, string move, IReadOnlyList<(int Sides, int Value)> dice) =>
        _gearService.ApplyToDamageAsync(character, move, dice);

    public async Task<IReadOnlyList<RollBonus>> BonusesAsync(Character character, RollKind kind, StatType? stat = null) =>
        SubclassLibrary.Bonuses(character, await _characterService.GetCharacterAbilitiesAsync(), kind, stat);

    public async Task<IReadOnlyList<RollBonus>> DamageBonusesAsync(Character character, int extra = 0)
    {
        var bonuses = (await BonusesAsync(character, RollKind.Damage)).ToList();
        if (character.DamageRating != 0)
            bonuses.Insert(0, new RollBonus("Gear damage", character.DamageRating));
        if (extra != 0)
            bonuses.Add(new RollBonus(ExtraSource, extra));
        return bonuses;
    }

    public async Task<PlayerHit> TakeAsync(Character character, int amount, DamageType type, int piercing = 0)
    {
        // Reckless Tactics: the Battlemaster takes +1d4 from every hit
        string? reckless = null;
        if (amount > 0 && TacticRules.ExtraDamage(character) is { } extra && _diceRoller.TryRoll(extra, out var extraRoll))
        {
            amount += extraRoll.Value;
            reckless = $"{TacticRules.Current(character)!.Name} Tactics: +{extraRoll.Value} ({extra}) damage taken";
        }

        // Modifiers can push a roll below zero ("1d4-3"), which shouldn't heal anyone
        return new PlayerHit(await _armorService.TakeDamageAsync(character, amount, type, piercing), reckless);
    }

    /// <summary>
    /// The kept, positive dice in a roll, with their sides, e.g. (8, 5) for a d8 that rolled 5.
    /// </summary>
    public static IReadOnlyList<(int Sides, int Value)> DamageDice(DiceResult roll) =>
        roll.Results
            .Where(d => d.AppliesToResultCalculation && d.Value > 0)
            .Select(d => (Sides: SidesOf(d.Type), d.Value))
            .Where(d => d.Sides > 1)
            .ToList();

    // Term types look like "DiceTerm.d8"; anything else (like fudge dice) counts as 0 sides
    private static int SidesOf(string type)
    {
        var name = type[(type.LastIndexOf('.') + 1)..];
        return name.StartsWith('d') && int.TryParse(name[1..], out var sides) ? sides : 0;
    }
}
