using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// How enemies and saved monsters are shown in Discord. Players only ever see an enemy's condition; the GM sees the rest.
/// </summary>
public static class EnemySheet
{
    /// <summary>
    /// Sorts "Goblin 2" before "Goblin 10".
    /// </summary>
    public static readonly IComparer<string> NameOrder = Comparer<string>.Create((a, b) =>
    {
        var (aBase, aNumber) = Split(a);
        var (bBase, bNumber) = Split(b);
        var byBase = string.Compare(aBase, bBase, StringComparison.OrdinalIgnoreCase);
        return byBase != 0 ? byBase : aNumber.CompareTo(bNumber);
    });

    private static (string Base, int Number) Split(string name)
    {
        var space = name.LastIndexOf(' ');
        return space > 0 && int.TryParse(name[(space + 1)..], out var number) ? (name[..space], number) : (name, 0);
    }

    /// <summary>
    /// Standing enemies in name order, then the defeated.
    /// </summary>
    public static IEnumerable<Enemy> Ordered(IEnumerable<Enemy> enemies) =>
        enemies.OrderBy(e => e.CurrentHP == 0).ThenBy(e => e.Name, NameOrder);

    public static string Emoji(EnemyCondition condition) => condition switch
    {
        EnemyCondition.Unhurt => "🟢",
        EnemyCondition.Hurt => "🟡",
        EnemyCondition.Bloodied => "🟠",
        EnemyCondition.NearDeath => "🔴",
        _ => "💀",
    };

    /// <summary>
    /// e.g. "🟠 Bloodied".
    /// </summary>
    public static string Condition(Enemy enemy) => $"{Emoji(enemy.Condition)} {EnemyConditions.Describe(enemy.Condition)}";

    /// <summary>
    /// One line for the GM's list, e.g. "❤️ 2/3 · 🛡️ 1 · ⚔️ d6 · Horde, Small".
    /// </summary>
    public static string Summary(Enemy enemy) =>
        Summary(enemy.CurrentHP, enemy.MaxHP, enemy.Armor, enemy.Damage, enemy.Tags);

    public static string Summary(MonsterTemplate monster) =>
        Summary(monster.MaxHP, monster.MaxHP, monster.Armor, monster.Damage, monster.Tags);

    private static string Summary(int hp, int maxHp, int armor, string? damage, string? tags)
    {
        var parts = new List<string> { $"❤️ {hp}/{maxHp}" };
        if (armor != 0)
            parts.Add($"🛡️ {armor}");
        if (damage is not null)
            parts.Add($"⚔️ {damage}");
        if (tags is not null)
            parts.Add(tags);

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// The whole fight as the GM sees it, one field per enemy.
    /// </summary>
    public static IReadOnlyList<IEmbedField> GameMasterFields(Encounter encounter) =>
        Ordered(encounter.Enemies)
            .Take(25)
            .Select(e => (IEmbedField)new EmbedField($"{Emoji(e.Condition)} {e.Name}", Details(e)))
            .ToList();

    /// <summary>
    /// The whole fight as the players see it: who's there and how they look.
    /// </summary>
    public static string PlayerList(Encounter encounter) =>
        encounter.Enemies.Count == 0
            ? "No enemies yet."
            : string.Join('\n', Ordered(encounter.Enemies).Select(e => $"**{e.Name}**: {Condition(e)}"));

    private static string Details(Enemy enemy)
    {
        List<string> lines = [Summary(enemy)];
        if (enemy.Instinct is not null)
            lines.Add($"*Instinct:* {enemy.Instinct}");
        if (enemy.Moves is not null)
            lines.AddRange(MoveLines(enemy.Moves));
        if (enemy.Notes is not null)
            lines.Add(enemy.Notes);

        // Discord rejects field values over 1024 characters
        var details = string.Join('\n', lines);
        return details.Length <= 1024 ? details : details[..1023] + "…";
    }

    public static IReadOnlyList<IEmbedField> Fields(MonsterTemplate monster)
    {
        List<IEmbedField> fields =
        [
            new EmbedField("HP", monster.MaxHP.ToString(), true),
            new EmbedField("Armor", monster.Armor.ToString(), true),
            new EmbedField("Damage", monster.Damage ?? "—", true),
        ];

        if (monster.Tags is not null)
            fields.Add(new EmbedField("Tags", monster.Tags));
        if (monster.Instinct is not null)
            fields.Add(new EmbedField("Instinct", monster.Instinct));
        if (monster.Moves is not null)
            fields.Add(new EmbedField("Moves", string.Join('\n', MoveLines(monster.Moves))));
        if (monster.Notes is not null)
            fields.Add(new EmbedField("Notes", monster.Notes));

        return fields;
    }

    /// <summary>
    /// Moves are typed into Discord on one line, separated by semicolons or new lines.
    /// </summary>
    public static IEnumerable<string> SplitMoves(string moves) =>
        moves.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<string> MoveLines(string moves) => SplitMoves(moves).Select(m => $"• {m}");
}
