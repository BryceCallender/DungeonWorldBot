using Remora.Discord.API.Abstractions.Objects;

namespace DungeonWorldBot.Features.Moves;

/// <summary>
/// A move's damage, and what it did to the enemy it was aimed at.
/// </summary>
/// <param name="Target">The enemy it hit, if there was one in the fight.</param>
/// <param name="Result">What the damage came to, e.g. "💥 4 damage" for an enemy or "💥 6 damage" with none.</param>
/// <param name="Taken">The damage that got through the enemy's armor, if it was applied to one.</param>
public record DamageDealt(IReadOnlyList<IEmbedField> Fields, string? Target = null, string? Result = null, int? Taken = null);
