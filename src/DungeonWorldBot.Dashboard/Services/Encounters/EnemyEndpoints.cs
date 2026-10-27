using DungeonWorldBot.Dashboard.Models.Auth;
using DungeonWorldBot.Dashboard.Services.Shared;
using DungeonWorldBot.Data.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DungeonWorldBot.Dashboard.Services.Encounters;

/// <summary>
/// The DM's forms on the enemies page: looking after the enemies in fights going on, and keeping the saved monsters
/// they're spawned from. Each goes back to the page, with the mistake (and the dialog to show it in) if there was one.
/// </summary>
public static class EnemyEndpoints
{
    private const string Page = "/enemies";

    public static RouteGroupBuilder MapEnemyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapPost("/enemies/{id:int}", async (int id, IFormCollection form, Viewer viewer, EncounterDesk desk) =>
        {
            if (!viewer.CanRunGame)
                return Results.Forbid();

            var details = new Enemy
            {
                CurrentHP = FormText.Number(form["currentHp"]) ?? 0,
                MaxHP = FormText.Number(form["maxHp"]) ?? 0,
                Armor = FormText.Number(form["armor"]) ?? 0,
                Damage = form["damage"],
                Tags = form["tags"],
                Instinct = form["instinct"],
                Moves = form["moves"],
                Notes = form["notes"],
            };
            return DeskForms.Back(Page, await desk.EditEnemyAsync(id, details), $"enemy-edit-{id}", $"enemy-{id}");
        });

        group.MapPost("/enemies/{id:int}/hp", async (int id, [FromForm] string? change, [FromForm] string? amount, Viewer viewer, EncounterDesk desk) =>
        {
            if (!viewer.CanRunGame)
                return Results.Forbid();
            if (DeskForms.Change(change) is not { } way)
                return Results.BadRequest();

            return DeskForms.Back(Page, await desk.ChangeEnemyHpAsync(id, way, FormText.Number(amount) ?? 0), anchor: $"enemy-{id}");
        });

        group.MapPost("/enemies/{id:int}/remove", async (int id, Viewer viewer, EncounterDesk desk) =>
        {
            if (!viewer.CanRunGame)
                return Results.Forbid();

            return DeskForms.Back(Page, await desk.RemoveEnemyAsync(id));
        });

        group.MapPost("/monsters", async (IFormCollection form, Viewer viewer, EncounterDesk desk) =>
        {
            if (!viewer.CanRunGame)
                return Results.Forbid();

            return DeskForms.Back(Page, await desk.SaveMonsterAsync(null, Monster(form)), "monster-new", "monsters");
        });

        group.MapPost("/monsters/{id:int}", async (int id, IFormCollection form, Viewer viewer, EncounterDesk desk) =>
        {
            if (!viewer.CanRunGame)
                return Results.Forbid();

            return DeskForms.Back(Page, await desk.SaveMonsterAsync(id, Monster(form)), $"monster-edit-{id}", $"monster-{id}");
        });

        group.MapPost("/monsters/{id:int}/remove", async (int id, Viewer viewer, EncounterDesk desk) =>
        {
            if (!viewer.CanRunGame)
                return Results.Forbid();

            return DeskForms.Back(Page, await desk.RemoveMonsterAsync(id), anchor: "monsters");
        });

        return group;
    }

    private static MonsterTemplate Monster(IFormCollection form) => new()
    {
        Name = form["name"].ToString(),
        MaxHP = FormText.Number(form["maxHp"]) ?? 0,
        Armor = FormText.Number(form["armor"]) ?? 0,
        Damage = form["damage"],
        Tags = form["tags"],
        Instinct = form["instinct"],
        Moves = form["moves"],
        Notes = form["notes"],
    };
}
