using DungeonWorldBot.Dashboard.Auth;
using DungeonWorldBot.Dashboard.Canvases;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using Microsoft.Data.Sqlite;

namespace DungeonWorldBot.Dashboard.Encounters;

/// <summary>
/// Who can change a scene's board. While a fight is on an encounter scene, only whoever runs fights and the players
/// the DM picked for it can; everyone else can still look, pan and zoom. Any other time, anyone can.
/// </summary>
public static class FightBoard
{
    public const string ViewOnlyMessage = "Only those in the fight can change this board while it's on.";

    /// <param name="fight">The fight on the scene, if there is one.</param>
    public static bool CanChange(Encounter? fight, Viewer viewer) =>
        fight is null
        || EncounterEndpoints.CanRun(viewer)
        // A fight the DM didn't pick anyone for (started in Discord) is everyone's
        || fight.Participants.Count == 0
        || fight.Participants.Any(p => p.CharacterID is { } id && viewer.Is(id.Value));

    public static async Task<bool> CanChangeAsync(Canvas scene, Viewer viewer, IEncounterService encounters)
    {
        if (scene.Kind != CanvasKind.Encounter || EncounterEndpoints.CanRun(viewer))
            return true;

        try
        {
            return CanChange(await encounters.GetOnSceneAsync(CanvasIds.Encode(scene.ID)), viewer);
        }
        catch (SqliteException e) when (e.Message.Contains("no such table") || e.Message.Contains("no such column"))
        {
            // The bot hasn't added fights to its database yet, so there's none to keep anyone out of
            return true;
        }
    }
}
