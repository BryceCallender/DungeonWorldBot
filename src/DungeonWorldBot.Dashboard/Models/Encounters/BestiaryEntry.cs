using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Dashboard.Models.Encounters;

/// <summary>
/// Every kind of monster the party has fought, and how that went.
/// </summary>
/// <param name="Name">The kind, like "Goblin" for Goblin 1 and Goblin 2.</param>
/// <param name="Met">How many of them the party has faced.</param>
/// <param name="Slain">How many of those were beaten down to 0 HP.</param>
/// <param name="Fights">How many fights they were in.</param>
/// <param name="DamageToParty">How much damage their attacks did to the party, from the fights' timelines.</param>
/// <param name="DamageTaken">How much damage the party's moves did to them.</param>
/// <param name="TopSlayer">The character who did the most of that damage, by their player's Discord ID.</param>
/// <param name="Template">The saved monster of the same name, for whoever runs fights to see its details.</param>
public record BestiaryEntry(
    string Name,
    int Met,
    int Slain,
    int Fights,
    DateTimeOffset FirstMet,
    int DamageToParty,
    int DamageTaken,
    ulong? TopSlayer,
    int TopSlayerDamage,
    MonsterTemplate? Template);
