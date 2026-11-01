namespace DungeonWorldBot.Dashboard.Models.Admin;

/// <summary>
/// Which of the dashboard's databases the admin pages are looking at: the bot's game, or the dashboard's scenes. Goes
/// in the address as "game" or "canvas".
/// </summary>
public enum AdminDatabase
{
    Game,
    Canvas,
}
