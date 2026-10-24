using DungeonWorldBot.Dashboard.Canvases;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Party;

/// <summary>
/// Characters' pictures, in the dashboard's own database.
/// </summary>
public class PortraitStore(CanvasContext db)
{
    /// <summary>
    /// Where a character's picture is served, with when it was set so a new one gets a new address.
    /// </summary>
    public static string Url(ulong characterID, DateTime updatedAt) => $"api/characters/{characterID}/portrait?v={updatedAt.Ticks}";

    public Task<CharacterPortrait?> FindAsync(ulong characterID) =>
        db.CharacterPortraits.AsNoTracking().FirstOrDefaultAsync(p => p.CharacterID == characterID);

    /// <summary>
    /// Every character with a picture, and its address, without loading the pictures.
    /// </summary>
    public async Task<Dictionary<ulong, string>> UrlsAsync() =>
        (await db.CharacterPortraits.Select(p => new { p.CharacterID, p.UpdatedAt }).ToListAsync())
        .ToDictionary(p => p.CharacterID, p => Url(p.CharacterID, p.UpdatedAt));

    public async Task SaveAsync(ulong characterID, string contentType, byte[] data)
    {
        var portrait = await db.CharacterPortraits.FindAsync(characterID);
        if (portrait is null)
        {
            portrait = new CharacterPortrait { CharacterID = characterID, ContentType = contentType, Data = data };
            db.CharacterPortraits.Add(portrait);
        }

        portrait.ContentType = contentType;
        portrait.Data = data;
        portrait.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task RemoveAsync(ulong characterID) =>
        await db.CharacterPortraits.Where(p => p.CharacterID == characterID).ExecuteDeleteAsync();
}
