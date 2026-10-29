using DungeonWorldBot.Features.Classes;

namespace DungeonWorldBot.Dashboard.Models.Encounters;

/// <summary>
/// Someone on a fight's replay: a character, an NPC or an enemy.
/// </summary>
/// <param name="Key">Who they are, like "enemy:12", matching the steps' actors and targets.</param>
/// <param name="Side">"party" or "enemies".</param>
/// <param name="MaxHP">Their max HP, so their health can go down as they're hurt. Null when it isn't known, like for
/// someone since taken out of the fight.</param>
/// <param name="Color">A character's class color, light and dark.</param>
public record ReplayFighter(string Key, string Name, string Side, string? Portrait, int? MaxHP, ClassColor? Color);
