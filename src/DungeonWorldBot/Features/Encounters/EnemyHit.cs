using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// What a player's or NPC's damage did to the enemy it was aimed at.
/// </summary>
/// <param name="Field">The roll's embed field saying what happened. Players only see how hurt the enemy looks.</param>
/// <param name="Target">The enemy, as named in the fight, if there was one.</param>
/// <param name="Result">e.g. "took 4", or null if the damage wasn't applied.</param>
public record EnemyHit(IEmbedField Field, string? Target = null, string? Result = null)
{
    /// <summary>
    /// Takes the damage off the enemy. The roll stands either way; if there's no such enemy, the DM can apply it by hand.
    /// </summary>
    public static async Task<EnemyHit> ApplyAsync(IEncounterService encounterService, string target, int damage)
    {
        if (await encounterService.FindEnemyAsync(target) is not { } enemy)
            return new EnemyHit(new EmbedField("Target", $"No enemy called {target.Trim()} is in the fight, so the damage wasn't applied."));

        if (enemy.CurrentHP == 0)
            return new EnemyHit(new EmbedField("Target", $"{enemy.Name} is already defeated, so the damage wasn't applied."), enemy.Name);

        var hit = await encounterService.DamageAsync(enemy, damage);
        var armor = hit.Blocked > 0 ? $" (armor stopped {hit.Blocked})" : string.Empty;
        return new EnemyHit(
            new EmbedField($"🎯 {enemy.Name}", $"Took **{hit.Taken}**{armor} · {EnemySheet.Condition(enemy)}"),
            enemy.Name,
            $"took {hit.Taken}");
    }
}
