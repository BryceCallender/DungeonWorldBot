using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Dashboard.Models.Encounters;

public record FightReplay(Encounter Fight, IReadOnlyList<ReplayFighter> Fighters, IReadOnlyList<ReplayStep> Steps);
