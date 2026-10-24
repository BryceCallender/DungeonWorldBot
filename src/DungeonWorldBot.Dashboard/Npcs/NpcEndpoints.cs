using DungeonWorldBot.Dashboard.Auth;
using DungeonWorldBot.Dashboard.Encounters;
using DungeonWorldBot.Dashboard.Shared;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using Microsoft.AspNetCore.Mvc;
using Remora.Rest.Core;

namespace DungeonWorldBot.Dashboard.Npcs;

/// <summary>
/// The DM's forms on the NPCs page: adding, changing, hurting, healing and sending away the party's NPCs. Each goes back
/// to the page, with the mistake (and the dialog to show it in) if there was one.
/// </summary>
public static class NpcEndpoints
{
    private const string Page = "/npcs";

    public static RouteGroupBuilder MapNpcEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/npcs");

        group.MapPost("/", async (IFormCollection form, Viewer viewer, NpcDesk desk, ICharacterService characters) =>
        {
            if (!EncounterEndpoints.CanRun(viewer))
                return Results.Forbid();

            return DeskForms.Back(Page, await desk.SaveAsync(null, await DetailsAsync(form, characters)), "npc-new");
        });

        group.MapPost("/{id:int}", async (int id, IFormCollection form, Viewer viewer, NpcDesk desk, ICharacterService characters) =>
        {
            if (!EncounterEndpoints.CanRun(viewer))
                return Results.Forbid();

            return DeskForms.Back(Page, await desk.SaveAsync(id, await DetailsAsync(form, characters)), $"npc-edit-{id}", $"npc-{id}");
        });

        group.MapPost("/{id:int}/hp", async (int id, [FromForm] string? change, [FromForm] string? amount, Viewer viewer, NpcDesk desk) =>
        {
            if (!EncounterEndpoints.CanRun(viewer))
                return Results.Forbid();
            if (DeskForms.Change(change) is not { } way)
                return Results.BadRequest();

            return DeskForms.Back(Page, await desk.ChangeHpAsync(id, way, FormText.Number(amount) ?? 0), anchor: $"npc-{id}");
        });

        group.MapPost("/{id:int}/remove", async (int id, Viewer viewer, NpcDesk desk) =>
        {
            if (!EncounterEndpoints.CanRun(viewer))
                return Results.Forbid();

            return DeskForms.Back(Page, await desk.RemoveAsync(id));
        });

        return group;
    }

    /// <summary>
    /// What the form says about the NPC. Who they follow is only taken when it's someone with a character.
    /// </summary>
    private static async Task<Npc> DetailsAsync(IFormCollection form, ICharacterService characters)
    {
        var npc = new Npc
        {
            Name = form["name"].ToString(),
            Role = form["role"],
            CurrentHP = FormText.Number(form["currentHp"]) ?? 0,
            MaxHP = FormText.Number(form["maxHp"]) ?? 0,
            Armor = FormText.Number(form["armor"]) ?? 0,
            Damage = form["damage"],
            Loyalty = FormText.Number(form["loyalty"]) ?? 0,
            Skills = form["skills"],
            Cost = form["cost"],
            Notes = form["notes"],
        };
        foreach (var stat in Enum.GetValues<StatType>())
            npc.SetModifier(stat, FormText.Number(form[stat.ToString().ToLowerInvariant()]) ?? 0);

        if (ulong.TryParse(form["follows"], out var follows)
            && (await characters.GetCharactersAsync()).Any(c => c.ID.Value == follows))
            npc.FollowsID = new Snowflake(follows);

        return npc;
    }
}
