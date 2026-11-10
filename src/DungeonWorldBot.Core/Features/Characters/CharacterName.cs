using DungeonWorldBot.Data.Entities;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters;

/// <summary>
/// A character's name and class, without the rest of the sheet.
/// </summary>
/// <param name="ID">Their player's user ID, which is also the character's.</param>
/// <param name="ControlledByID">Whoever has taken the character over for now, if anyone.</param>
public record CharacterName(Snowflake ID, string Name, ClassType Class, Snowflake? ControlledByID = null);
