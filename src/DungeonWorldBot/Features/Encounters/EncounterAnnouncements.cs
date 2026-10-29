using System.Drawing;
using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// What game chat is told when a fight starts or ends, wherever it was started from.
/// </summary>
public static class EncounterAnnouncements
{
    private static readonly Color StartColor = Color.FromArgb(0xD1, 0x45, 0x3B);
    private static readonly Color EndColor = Color.FromArgb(0x2F, 0x9E, 0x5B);

    public static Embed Started(Encounter encounter)
    {
        var description = encounter.SceneName is { } scene
            ? encounter.SceneUrl is { } url ? $"On [{scene}]({url})." : $"On {scene}."
            : "Roll for it!";

        return new Embed(
            Title: $"⚔️ A Fight Begins: {encounter.Name}",
            Description: description,
            Url: encounter.SceneUrl is { } link ? link : default(Optional<string>),
            Colour: StartColor);
    }

    /// <param name="cleared">What ending the fight took off everyone, as worded by /armor end-fight.</param>
    public static Embed Ended(Encounter encounter, string cleared)
    {
        var defeated = encounter.Enemies.Count(e => e.CurrentHP == 0);
        var summary = encounter.Enemies.Count == 0
            ? $"**{encounter.Name}** is over."
            : $"**{encounter.Name}** is over. {defeated} of {encounter.Enemies.Count} enemies defeated.";

        return new Embed(
            Title: "Fight Over",
            Description: $"{summary}\n\n{cleared}",
            Colour: EndColor);
    }
}
