namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// A fight. Several can go on at once, each until it's ended.
/// </summary>
public class Encounter
{
    public int ID { get; set; }

    public string Name { get; set; } = null!;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// The dashboard scene it's fought on, by its short ID (see <see cref="CanvasIds"/>). Null when it isn't on one.
    /// A scene has one fight going on at a time.
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
    /// The characters and NPCs the DM picked to be in it. None means everyone, as with fights started in Discord.
    /// </summary>
    public List<EncounterParticipant> Participants { get; set; } = new();

    /// <summary>
    /// What everyone did in the fight, oldest first by ID.
    /// </summary>
    public List<EncounterAction> Actions { get; set; } = new();
}
