using DungeonWorldBot.Features.Characters;

namespace DungeonWorldBot.Dashboard.Models.Party;

/// <summary>
/// Who a player is playing, as the bot decides for their commands: a character they've taken over with /character
/// takeover, or else their own.
/// </summary>
/// <param name="Playing">The character their commands act on, or null when they have none.</param>
/// <param name="Others">Their other character: their own, while they're playing someone else's.</param>
public record PlayedCharacters(CharacterName? Playing, IReadOnlyList<CharacterName> Others)
{
    public bool Plays(ulong characterID) => Playing?.ID.Value == characterID;

    /// <summary>
    /// By the bot's rule for their commands (see CharacterService): the one character they've taken over, or else
    /// their own.
    /// </summary>
    public static PlayedCharacters Of(IReadOnlyList<CharacterName> characters, ulong userID)
    {
        var own = characters.FirstOrDefault(c => c.ID.Value == userID);
        var taken = characters.Where(c => c.ControlledByID?.Value == userID).ToList();
        return taken is [var playing]
            ? new PlayedCharacters(playing, own is null ? [] : [own])
            : new PlayedCharacters(own, []);
    }
}
