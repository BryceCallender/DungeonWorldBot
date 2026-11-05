using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Combat;

/// <summary>
/// The temporary armor a character lost when a fight ended.
/// </summary>
public record ClearedArmor(string Name, IReadOnlyList<ArmorBonus> Removed);
