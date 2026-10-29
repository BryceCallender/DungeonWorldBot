using DungeonWorldBot.Dashboard.Helpers;

namespace DungeonWorldBot.Dashboard.Models.Encounters;

/// <summary>
/// One thing someone did in the fight, in order.
/// </summary>
/// <param name="At">How far into the fight, like "4m".</param>
/// <param name="Damage">What it did to the target, from its result ("💥 4 damage").</param>
public record ReplayStep(string At, string? Actor, string ActorName, string Action, string? Roll, string? Target, string? TargetName, string? Result, int Damage)
{
    /// <summary>
    /// The move and who it was aimed at, e.g. "Volley → Goblin 2".
    /// </summary>
    public string Line => TargetName is null ? Action : $"{Action} → {TargetName}";

    /// <summary>
    /// The roll's pill colour (see <see cref="RollStyles.FromRollText"/>).
    /// </summary>
    public string? RollClass => Roll is null ? null : RollStyles.FromRollText(Roll);

    public string? ShortRoll => Roll is null ? null : Format.ShortFightText(Roll);

    public string? ShortResult => Result is null ? null : Format.ShortFightText(Result);
}
