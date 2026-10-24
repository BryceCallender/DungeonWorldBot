using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Rules;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;

namespace DungeonWorldBot.Features.Npcs;

/// <summary>
/// How NPCs are shown in Discord.
/// </summary>
public static class NpcSheet
{
    /// <summary>
    /// e.g. "Grub took 3 damage! (5/8 HP)" with the armor's share under it.
    /// </summary>
    public static string DescribeHit(Npc npc, ArmoredHit hit)
    {
        var description = $"{npc.Name} took {hit.Taken} damage! ({npc.CurrentHP}/{npc.MaxHP} HP)" +
                          (npc.CurrentHP == 0 ? " · they're down" : string.Empty);
        if (hit.Blocked > 0)
            description += $"\nRolled {hit.Rolled}, armor stopped {hit.Blocked}";
        return description;
    }

    public static string Title(Npc npc) => npc.Role is null ? npc.Name : $"{npc.Name} · {npc.Role}";

    /// <summary>
    /// One line for lists, e.g. "❤️ 6/8 · 🛡️ 1 · ⚔️ d6 · Loyalty +1".
    /// </summary>
    public static string Summary(Npc npc)
    {
        var parts = new List<string> { $"❤️ {npc.CurrentHP}/{npc.MaxHP}" };
        if (npc.Armor != 0)
            parts.Add($"🛡️ {npc.Armor}");
        if (npc.Damage is not null)
            parts.Add($"⚔️ {npc.Damage}");
        if (npc.Loyalty != 0)
            parts.Add($"Loyalty {Signed(npc.Loyalty)}");
        if (npc.FollowsID is { } follows)
            parts.Add($"follows <@{follows.Value}>");
        if (npc.CurrentHP == 0)
            parts.Add("**down**");

        return string.Join(" · ", parts);
    }

    public static IReadOnlyList<IEmbedField> Fields(Npc npc)
    {
        var fields = new List<IEmbedField>
        {
            new EmbedField("HP", $"{npc.CurrentHP}/{npc.MaxHP}", true),
            new EmbedField("Armor", npc.Armor.ToString(), true),
            new EmbedField("Damage", npc.Damage ?? "—", true),
            new EmbedField("Loyalty", Signed(npc.Loyalty), true),
            new EmbedField("Stats", Stats(npc)),
        };

        if (npc.FollowsID is { } follows)
            fields.Add(new EmbedField("Follows", $"<@{follows.Value}>", true));
        if (npc.Skills is not null)
            fields.Add(new EmbedField("Skills", npc.Skills));
        if (npc.Cost is not null)
            fields.Add(new EmbedField("Cost", npc.Cost));
        if (npc.Notes is not null)
            fields.Add(new EmbedField("Notes", npc.Notes));

        return fields;
    }

    /// <summary>
    /// e.g. "STR +2 · DEX +0 · CON +1 · INT -1 · WIS +0 · CHA +0".
    /// </summary>
    public static string Stats(Npc npc) =>
        string.Join(" · ", Enum.GetValues<StatType>().Select(s => $"{s.ToString().ToUpper()} {Signed(npc.Modifier(s))}"));

    public static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();
}
