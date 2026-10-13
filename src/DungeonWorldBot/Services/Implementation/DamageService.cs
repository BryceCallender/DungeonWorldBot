using d20Tek.DiceNotation.Results;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Helpers;
using DungeonWorldBot.Rolling;
using DungeonWorldBot.Subclasses;

namespace DungeonWorldBot.Services.Implementation;

public class DamageService : IDamageService
{
    private readonly ICharacterService _characterService;
    private readonly IDiceRoller _diceRoller;
    private readonly IGearService _gearService;

    public DamageService(ICharacterService characterService, IDiceRoller diceRoller, IGearService gearService)
    {
        _characterService = characterService;
        _diceRoller = diceRoller;
        _gearService = gearService;
    }

    public async Task<DamageRoll?> RollAsync(Character character, string move)
    {
        var bonuses = await DamageBonusesAsync(character);
        var expression = DiceHelper.AddModifier(character.DamageModifier ?? character.Class.Damage, bonuses.Sum(b => b.Amount));
        if (!_diceRoller.TryRoll(expression, out var dice))
            return null;

        var gear = await ApplyGearAsync(character, move, DamageDice(dice));
        return new DamageRoll(expression, dice, dice.Value + gear.Bonus, bonuses, gear);
    }

    public Task<GearDamage> ApplyGearAsync(Character character, string move, IReadOnlyList<(int Sides, int Value)> dice) =>
        _gearService.ApplyToDamageAsync(character, move, dice);

    public async Task<IReadOnlyList<RollBonus>> BonusesAsync(Character character, RollKind kind, StatType? stat = null) =>
        SubclassLibrary.Bonuses(character, await _characterService.GetCharactersAsync(), kind, stat);

    public async Task<IReadOnlyList<RollBonus>> DamageBonusesAsync(Character character)
    {
        var bonuses = await BonusesAsync(character, RollKind.Damage);
        return character.DamageRating != 0
            ? bonuses.Prepend(new RollBonus("Gear damage", character.DamageRating)).ToList()
            : bonuses;
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
