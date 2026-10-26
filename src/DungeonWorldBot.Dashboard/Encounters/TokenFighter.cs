namespace DungeonWorldBot.Dashboard.Encounters;

public enum FighterKind
{
    Enemy,
    Character,
    Npc,
}

/// <summary>
/// Who a token on a fight's board is, kept in its fighter attribute as e.g. "enemy:12", "character:2038..." or "npc:3",
/// so moves made from it reach the right one whatever the token's called.
/// </summary>
/// <param name="ID">The enemy's or NPC's ID, or the character's player's Discord ID.</param>
public record TokenFighter(FighterKind Kind, string ID)
{
    public const string Attribute = "fighter";

    public static TokenFighter Enemy(int id) => new(FighterKind.Enemy, id.ToString());

    public static TokenFighter Character(ulong id) => new(FighterKind.Character, id.ToString());

    public static TokenFighter Npc(int id) => new(FighterKind.Npc, id.ToString());

    public override string ToString() => $"{Kind.ToString().ToLowerInvariant()}:{ID}";

    public static TokenFighter? Parse(string? value)
    {
        var parts = value?.Split(':', 2);
        return parts is [var kind, var id] && Enum.TryParse<FighterKind>(kind, ignoreCase: true, out var parsed) && ulong.TryParse(id, out _)
            ? new TokenFighter(parsed, id)
            : null;
    }
}
