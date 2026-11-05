namespace DungeonWorldBot.Dashboard.Services.Shared;

/// <summary>
/// A picture someone uploaded, for a board, a token or a character: checked to really be one by its first bytes, and
/// never decoded.
/// </summary>
public record Picture(string ContentType, byte[] Data)
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public const string NotAPicture = "That isn't a PNG, JPEG, WebP or GIF.";

    public static string TooBig => $"Pictures can be up to {MaxBytes / 1024 / 1024} MB.";

    /// <returns>The picture, or null when the file isn't one.</returns>
    public static async Task<Picture?> ReadAsync(IFormFile file)
    {
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer);
        var data = buffer.ToArray();
        return TypeOf(data) is { } contentType ? new Picture(contentType, data) : null;
    }

    // SVG isn't allowed: it can carry scripts
    private static string? TypeOf(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]))
            return "image/png";
        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
            return "image/jpeg";
        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8))
            return "image/gif";
        if (data.Length > 12 && data.StartsWith("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }
}
