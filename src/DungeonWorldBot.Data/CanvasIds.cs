using Sqids;

namespace DungeonWorldBot.Data;

/// <summary>
/// The short IDs canvases go by in links and on the page, like Xb3kQ9. The database keeps plain numbers.
/// </summary>
/// <remarks>
/// They only make links tidier and harder to count through; they aren't secret. Changing the alphabet changes every
/// canvas's link.
/// </remarks>
public static class CanvasIds
{
    private static readonly SqidsEncoder<int> Encoder = new(new SqidsOptions
    {
        Alphabet = "5f68crAZY9KopGJibun3jNdlQz4qeSLyIPHXawVxF70UMvskOthERm12DWCgTB",
        MinLength = 6,
    });

    public static string Encode(int id) => Encoder.Encode(id);

    public static bool TryDecode(string? sqid, out int id)
    {
        id = 0;
        if (string.IsNullOrEmpty(sqid))
            return false;

        // Several strings can decode to the same number, so only the one Encode makes counts
        var numbers = Encoder.Decode(sqid);
        if (numbers.Count != 1 || Encoder.Encode(numbers[0]) != sqid)
            return false;

        id = numbers[0];
        return true;
    }
}
