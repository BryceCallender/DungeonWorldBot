namespace DungeonWorldBot.Dashboard.Models.Encounters;

/// <summary>
/// One thing someone did in the fight, in order.
/// </summary>
/// <param name="At">How far into the fight, like "4m".</param>
/// <param name="Damage">What it did to the target, from its result ("💥 4 damage").</param>
public record ReplayStep(string At, string? Actor, string ActorName, string Action, string? Roll, string? Target, string? TargetName, string? Result, int Damage);
