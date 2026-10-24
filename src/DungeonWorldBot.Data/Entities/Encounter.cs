namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// A fight. Only one runs at a time: the one that hasn't ended.
/// </summary>
public class Encounter
{
    public int ID { get; set; }

    public string Name { get; set; } = null!;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// The dashboard scene it's fought on, by its short ID (see <see cref="CanvasIds"/>). Null when it isn't on one,
    /// and then every encounter scene shows it.
    /// </summary>
    public string? SceneID { get; set; }

    public string? SceneName { get; set; }

    /// <summary>
    /// The scene's full address, for links in Discord.
    /// </summary>
    public string? SceneUrl { get; set; }

    /// <summary>
    /// When the bot told game chat it started. Fights started from the dashboard wait for the bot to notice them;
    /// fights started in Discord are announced by the command's own reply.
    /// </summary>
    public DateTimeOffset? AnnouncedAt { get; set; }

    /// <summary>
    /// When the bot told game chat it ended, and cleared the fight's temporary armor.
    /// </summary>
    public DateTimeOffset? EndAnnouncedAt { get; set; }

    public List<Enemy> Enemies { get; set; } = new();

    /// <summary>
    /// What everyone did in the fight, oldest first by ID.
    /// </summary>
    public List<EncounterAction> Actions { get; set; } = new();
}
