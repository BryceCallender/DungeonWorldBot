using DungeonWorldBot.Dashboard.Auth;
using DungeonWorldBot.Dashboard.Canvases;
using DungeonWorldBot.Dashboard.Components;
using DungeonWorldBot.Dashboard.Party;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Encounters;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace DungeonWorldBot.Dashboard.Encounters;

/// <summary>
/// The fight on an encounter scene: its panel for the page to swap in, and the DM's forms for starting, filling and
/// ending it. Each form goes back to the scene, with the mistake (and the dialog to show it in) if there was one.
/// </summary>
public static class EncounterEndpoints
{
    public static RouteGroupBuilder MapEncounterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scenes/{id}/encounter");

        group.MapGet("/panel", (string id) =>
            new RazorComponentResult<EncounterPanel>(new { SceneID = id }));

        group.MapPost("/start", async (
            string id,
            IFormCollection form,
            HttpRequest request,
            CanvasStore store,
            Viewer viewer,
            EncounterDesk desk,
            ICharacterService characters,
            PartyReader party,
            IEncounterService encounters,
            PortraitStore portraitStore,
            IHubContext<CanvasHub, ICanvasClient> hub) =>
        {
            if (!CanRun(viewer))
                return Results.Forbid();
            if (await EncounterSceneAsync(id, store) is not { } scene)
                return Results.NotFound();

            // Each saved monster's count follows its name in the form
            var monsters = form["monster"].Zip(form["count"], (monster, count) => new MonsterCount(monster ?? "", Number(count) ?? 0)).ToList();

            // Only real names are taken, so the form can't put anyone else in the fight
            var picked = form["party"].ToHashSet(StringComparer.OrdinalIgnoreCase);
            var pickedNpcs = form["npc"].ToHashSet(StringComparer.OrdinalIgnoreCase);
            var fighters = (await characters.GetCharactersAsync()).Where(c => picked.Contains(c.Name)).OrderBy(c => c.Name).ToList();
            var npcs = (await party.GetNpcsAsync()).Where(n => pickedNpcs.Contains(n.Name)).ToList();

            // Nobody can be in two fights at once
            var taken = await encounters.GetTakenAsync();
            var busy = fighters.Where(c => taken.Characters.Contains(c.ID)).Select(c => c.Name)
                .Concat(npcs.Where(n => taken.Npcs.Contains(n.ID)).Select(n => n.Name))
                .ToList();
            if (busy.Count > 0)
                return Back(id, "start-fight", $"{string.Join(", ", busy)} {(busy.Count == 1 ? "is" : "are")} already in another fight.");

            var url = $"{request.Scheme}://{request.Host}{request.PathBase}/scene/{id}";
            var started = await desk.StartAsync(
                form["name"],
                new SceneLink(id, scene.Name, url),
                new EncounterRoster(fighters.Select(c => c.ID).ToList(), npcs.Select(n => n.ID).ToList()),
                monsters);
            if (started.Error is not null)
                return Back(id, "start-fight", started.Error);

            // Everyone in it gets a token on the board, for everyone looking at it, with their picture if they have one
            var portraits = await portraitStore.UrlsAsync();
            await EnemyTokens.PlacePartyAsync(
                scene.ID,
                fighters.Select(c => new NewToken(c.Name, portraits.GetValueOrDefault(c.ID.Value))).ToList(),
                npcs.Select(n => n.Name).ToList(),
                store,
                hub);
            await EnemyTokens.PlaceAsync(scene.ID, started.Names, store, hub);

            return Back(id, "start-fight", null);
        });

        group.MapPost("/enemies", async (
            string id,
            [FromForm] string? from,
            [FromForm] string? monster,
            [FromForm] string? count,
            [FromForm] string? name,
            [FromForm] string? maxHp,
            [FromForm] string? armor,
            [FromForm] string? damage,
            [FromForm] string? tags,
            [FromForm] string? instinct,
            [FromForm] string? moves,
            [FromForm] string? notes,
            [FromForm] string? save,
            [FromForm] string? pictureUrl,
            CanvasStore store,
            Viewer viewer,
            EncounterDesk desk,
            IHubContext<CanvasHub, ICanvasClient> hub) =>
        {
            if (!CanRun(viewer))
                return Results.Forbid();
            if (await EncounterSceneAsync(id, store) is not { } scene)
                return Results.NotFound();

            var added = from == "saved"
                ? await desk.AddMonsterAsync(id, monster, Number(count) ?? 1)
                : await desk.AddCustomAsync(id, new MonsterTemplate
                {
                    Name = name ?? "",
                    MaxHP = Number(maxHp) ?? 0,
                    Armor = Number(armor) ?? 0,
                    Damage = damage,
                    Tags = tags,
                    Instinct = instinct,
                    Moves = moves,
                    Notes = notes,
                }, save is not null);

            // Each one gets a token on the board, for everyone looking at it, with the picture if one came along
            var picture = await ScenePictureAsync(id, scene.ID, pictureUrl, store);
            await EnemyTokens.PlaceAsync(scene.ID, added.Names, store, hub, picture);

            // A mistake in a new monster reopens the dialog on writing one up, not on the saved ones
            return Back(id, "add-enemies", added.Error, from == "saved" ? null : "new");
        });

        group.MapPost("/end", async (string id, CanvasStore store, Viewer viewer, EncounterDesk desk) =>
        {
            if (!CanRun(viewer))
                return Results.Forbid();
            if (await EncounterSceneAsync(id, store) is null)
                return Results.NotFound();

            return Back(id, "end-fight", await desk.EndAsync(id));
        });

        return group;
    }

    /// <summary>
    /// Who runs fights: the DM, or anyone when sign-in is off, as with archiving scenes.
    /// </summary>
    public static bool CanRun(Viewer viewer) => !viewer.SignedIn || viewer.IsDungeonMaster;

    private static async Task<Canvas?> EncounterSceneAsync(string id, CanvasStore store) =>
        CanvasIds.TryDecode(id, out var canvasID) && await store.FindAsync(canvasID) is { Kind: CanvasKind.Encounter, ArchivedAt: null } scene
            ? scene
            : null;

    /// <summary>
    /// The picture the dialog uploaded for the enemies' tokens, if it's really one of this scene's pictures. Anything
    /// else is ignored, so a token can't be pointed somewhere else.
    /// </summary>
    private static async Task<string?> ScenePictureAsync(string sqid, int canvasID, string? url, CanvasStore store)
    {
        var prefix = $"api/canvases/{sqid}/images/";
        if (url is null || !url.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        return await store.FindImageAsync(canvasID, url[prefix.Length..]) is null ? null : url;
    }

    // Number fields come through as text, and blank when left empty
    private static int? Number(string? value) => int.TryParse(value, out var number) ? number : null;

    private static IResult Back(string id, string dialog, string? error, string? from = null) =>
        Results.LocalRedirect(error is null
            ? $"/scene/{id}"
            : $"/scene/{id}?dialog={dialog}{(from is null ? "" : $"&from={from}")}&fightError={Uri.EscapeDataString(error)}");
}
