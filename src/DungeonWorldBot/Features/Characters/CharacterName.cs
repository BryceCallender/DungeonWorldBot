using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Characters;

/// <summary>
/// A character's name and class, without the rest of the sheet.
/// </summary>
public record CharacterName(string Name, ClassType Class);
