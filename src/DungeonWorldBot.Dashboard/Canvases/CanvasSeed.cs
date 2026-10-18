using System.Text.Json;

namespace DungeonWorldBot.Dashboard.Canvases;

/// <summary>
/// Made-up canvases for trying out the pages, added with SeedCanvases=true while there are none yet.
/// </summary>
public static class CanvasSeed
{
    public static void Seed(CanvasContext db, string? dungeonMaster)
    {
        if (db.Canvases.Any())
            return;

        var now = DateTime.UtcNow;

        // Something of each kind, edited from moments to weeks ago, with a long name and an empty one to test the cards
        var canvases = new List<Canvas>
        {
            New("Goblin ambush at the ford", CanvasKind.Encounter, now.AddMinutes(-2), now.AddHours(-3), dungeonMaster,
                Stroke("#5d9b4a", 40, 220, 120, 180, 220, 200, 320, 160, 420, 190, 520, 150),
                Stroke("#3f7fbf", 30, 300, 200, 310, 380, 290, 560, 320),
                Token("Kael", "#1D9E75", 160, 140),
                Token("Mira", "#7F77DD", 200, 260),
                Token("Goblin boss", "#D85A30", 420, 120),
                Token("Goblin", "#D85A30", 470, 200),
                Token("Goblin", "#D85A30", 380, 230),
                Text("Ford (difficult terrain)", 260, 330)),
            New("The Sunken Abbey", CanvasKind.Map, now.AddHours(-20), now.AddDays(-6), dungeonMaster,
                Stroke("#888780", 60, 60, 400, 60, 400, 300, 60, 300, 60, 60),
                Stroke("#888780", 230, 60, 230, 180, 400, 180),
                Stroke("#378ADD", 80, 280, 160, 240, 260, 270, 380, 250),
                Text("Flooded nave", 120, 120),
                Text("Crypt stairs", 300, 220)),
            New("Owlbear den", CanvasKind.Encounter, now.AddDays(-3), now.AddDays(-3), null,
                Stroke("#BA7517", 100, 100, 180, 160, 260, 120, 300, 200),
                Token("Owlbear", "#D85A30", 260, 160),
                Token("Steve", "#534AB7", 120, 220)),
            New("Faction web: who owes what to the Ashen Court and why nobody trusts the harbourmaster", CanvasKind.Notes,
                now.AddDays(-9), now.AddDays(-30), dungeonMaster,
                Stroke("#7F77DD", 100, 100, 300, 200),
                Stroke("#7F77DD", 300, 200, 480, 90),
                Stroke("#D4537E", 300, 200, 300, 320),
                Text("Ashen Court", 80, 80),
                Text("Harbourmaster", 460, 70),
                Text("Thieves' guild", 280, 340)),
            New("Blank map for next session", CanvasKind.Map, now.AddHours(-1), now.AddHours(-1), dungeonMaster),

            // Put away, for the archived list
            Archived(now.AddDays(-35), New("Bandit toll bridge", CanvasKind.Encounter, now.AddDays(-40), now.AddDays(-41), dungeonMaster,
                Stroke("#888780", 50, 200, 550, 200),
                Token("Bandit", "#D85A30", 300, 180))),
            Archived(now.AddDays(-50), New("Old tavern brawl", CanvasKind.Encounter, now.AddDays(-60), now.AddDays(-60), null,
                Token("Ren", "#1D9E75", 100, 100))),
        };

        db.Canvases.AddRange(canvases);
        db.SaveChanges();
    }

    private static Canvas New(string name, CanvasKind kind, DateTime updated, DateTime created, string? createdBy,
        params (CanvasItemType Type, object Json)[] items)
    {
        var canvas = new Canvas { Name = name, Kind = kind, CreatedBy = createdBy, CreatedAt = created, UpdatedAt = updated };
        for (var i = 0; i < items.Length; i++)
            canvas.Items.Add(new CanvasItem
            {
                ID = $"seed-{i}",
                Type = items[i].Type,
                Json = JsonSerializer.Serialize(items[i].Json),
                AuthorID = createdBy ?? "seed",
                // Apart, so they keep their order
                CreatedAt = created.AddSeconds(i),
            });
        return canvas;
    }

    private static Canvas Archived(DateTime at, Canvas canvas)
    {
        canvas.ArchivedAt = at;
        return canvas;
    }

    // Shaped like Konva's toObject(), so the page can draw them with Konva.Node.create
    private static (CanvasItemType, object) Stroke(string color, params double[] points) =>
        (CanvasItemType.Stroke, new
        {
            className = "Line",
            attrs = new { points, stroke = color, strokeWidth = 4, lineCap = "round", lineJoin = "round", tension = 0.5 },
        });

    private static (CanvasItemType, object) Token(string name, string color, double x, double y) =>
        (CanvasItemType.Token, new
        {
            className = "Circle",
            attrs = new { x, y, radius = 16, fill = color, name, draggable = true },
        });

    private static (CanvasItemType, object) Text(string text, double x, double y) =>
        (CanvasItemType.Text, new
        {
            className = "Text",
            attrs = new { x, y, text, fontSize = 16, fill = "#444441" },
        });
}
