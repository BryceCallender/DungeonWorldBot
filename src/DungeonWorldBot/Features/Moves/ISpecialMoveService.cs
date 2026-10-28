using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Moves;

/// <summary>
/// What making camp did.
/// </summary>
/// <param name="Ration">The ration eaten, or null if they had none.</param>
/// <param name="RationsLeft">How many of that ration are left.</param>
/// <param name="Healed">HP gained from sleeping, after capping at max HP.</param>
public record CampResult(string? Ration, int RationsLeft, int Healed);

/// <summary>
/// What resting did.
/// </summary>
/// <param name="Removed">The debilities cleared.</param>
/// <param name="StillToRemove">Debilities they've earned the removal of but haven't picked yet.</param>
public record RecoveryResult(int Healed, IReadOnlyList<DebilityType> Removed, int StillToRemove);

public interface ISpecialMoveService
{
    /// <summary>
    /// Make Camp: eat a ration, then heal half max HP if they got a few uninterrupted hours of sleep.
    /// </summary>
    Task<CampResult> MakeCampAsync(Character character, bool slept);

    /// <summary>
    /// Recover: a day of rest heals all HP, and every three days (two under a healer) clears a debility.
    /// Clears them all if they've earned enough, otherwise only the one they chose.
    /// </summary>
    Task<RecoveryResult> RecoverAsync(Character character, int days, bool healer, DebilityType? chosen);

    /// <returns>False, spending nothing, if they don't have that many coins.</returns>
    Task<bool> SpendCoinsAsync(Character character, int amount);
}
