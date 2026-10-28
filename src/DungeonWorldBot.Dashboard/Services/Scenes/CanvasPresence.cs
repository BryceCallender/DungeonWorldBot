using System.Collections.Concurrent;
using DungeonWorldBot.Dashboard.Models.Scenes;
using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Dashboard.Services.Scenes;

/// <summary>
/// Who has which canvas open, by connection. One person can have the same canvas open in several tabs.
/// </summary>
public class CanvasPresence
{
    private readonly ConcurrentDictionary<string, (int Canvas, CanvasPerson Person)> connections = new();

    /// <returns>The canvas the connection was on before, if any.</returns>
    public int? Join(string connectionID, int canvas, CanvasPerson person)
    {
        int? previous = null;
        connections.AddOrUpdate(connectionID,
            _ => (canvas, person),
            (_, old) => { previous = old.Canvas; return (canvas, person); });
        return previous == canvas ? null : previous;
    }

    /// <returns>The canvas the connection was on, if any.</returns>
    public int? Leave(string connectionID) =>
        connections.TryRemove(connectionID, out var left) ? left.Canvas : null;

    public (int Canvas, CanvasPerson Person)? Find(string connectionID) =>
        connections.TryGetValue(connectionID, out var found) ? found : null;

    /// <returns>Everyone on the canvas, once each however many tabs they have open.</returns>
    public IReadOnlyList<CanvasPerson> On(int canvas) =>
        connections.Values
            .Where(c => c.Canvas == canvas)
            .Select(c => c.Person)
            .DistinctBy(p => p.ID)
            .OrderBy(p => p.Name)
            .ToList();
}
