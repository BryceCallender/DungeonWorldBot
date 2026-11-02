using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Rolls;

namespace DungeonWorldBot.Dashboard.Models.Rolls;

/// <summary>
/// One roll as the feed shows it. IDs are strings, since Discord's are too big for JavaScript numbers.
/// </summary>
/// <param name="Who">Their character's name, or "The DM".</param>
/// <param name="Portrait">Their character's picture, if they've set one.</param>
/// <param name="Mine">Whether the person looking rolled it.</param>
/// <param name="Stat">The stat rolled with, like "+STR".</param>
/// <param name="Outcome">"strong", "weak" or "miss".</param>
/// <param name="Streak">The streak this roll puts them on, like "🔥 4 hits in a row", if it's long enough to mention.</param>
/// <param name="Hot">Whether that streak is hits rather than misses.</param>
/// <param name="Color">Their character's class color, light and dark, which their dice are drawn in.</param>
/// <param name="Drama">What makes it dramatic, like "lastbreathdied" or "comeback" (see <see cref="DramaticRolls"/>), so it
/// lands slower and louder.</param>
/// <param name="Headline">What the bot headlined it with in Discord, like "⚖️ Death offers a bargain".</param>
public record FeedRoll(
    string ID,
    string Who,
    string? Portrait,
    bool IsDungeonMaster,
    bool Mine,
    int First,
    int Second,
    int Total,
    string? Move,
    string? Stat,
    string Outcome,
    string? Streak = null,
    bool? Hot = null,
    ClassColor? Color = null,
    string? Drama = null,
    string? Headline = null);
