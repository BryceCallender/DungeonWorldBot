using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Gear;

/// <summary>
/// What a character's gear did to a damage roll.
/// </summary>
/// <param name="Bonus">Damage added, e.g. from a Sapphire reroll.</param>
/// <param name="Notes">Reminders and changes to show with the damage, one per line.</param>
public sealed record GearDamage(int Bonus, IReadOnlyList<string> Notes)
{
    public static readonly GearDamage None = new(0, []);
}

public interface IGearService
{
    Task GiveAsync(Character character, CustomGear gear);

    /// <summary>
    /// Takes the gear away, clearing its counters.
    /// </summary>
    Task RemoveAsync(Character character, CustomGear gear);

    /// <summary>
    /// Switches the gear's mode (like socketing a different gem). The old mode's counter is cleared.
    /// </summary>
    Task SetModeAsync(Character character, CustomGear gear, GearMode mode);

    int GetCounter(Character character, GearCounter counter);

    /// <returns>The counter's new value, kept between 0 and its max.</returns>
    Task<int> SetCounterAsync(Character character, GearCounter counter, int value);

    /// <summary>
    /// Applies the character's gear to a damage roll for a move: rerolls, reminders, and counters gained from dealing damage.
    /// </summary>
    /// <param name="dice">Each damage die's sides and result.</param>
    Task<GearDamage> ApplyToDamageAsync(Character character, string move, IReadOnlyList<(int Sides, int Value)> dice);
}
