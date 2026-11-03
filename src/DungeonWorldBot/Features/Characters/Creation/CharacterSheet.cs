using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Rules;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;

namespace DungeonWorldBot.Features.Characters.Creation;

public static class CharacterSheet
{
    // Embed field values are capped at 1024 characters
    private const int MaxFieldValue = 1024;

    /// <summary>
    /// The fields shown for a newly created character, both while reviewing it and once it's saved.
    /// </summary>
    public static List<IEmbedField> NewCharacterFields(Character character)
    {
        List<IEmbedField> fields =
        [
            new EmbedField("Race", character.Race.ToString(), IsInline: true),
            new EmbedField("Alignment", character.Alignment.ToString(), IsInline: true),
        ];

        fields.AddRange(character.Stats.Select(s => new EmbedField(
            CreationView.StatName(s.StatType), $"{s.Value} ({CreationView.FormatModifier(s.Modifier)})", IsInline: true)));

        fields.Add(new EmbedField("Health", character.Health.ToDisplay(), IsInline: true));
        fields.Add(new EmbedField("Armor", DamageRules.Summary(character), IsInline: true));
        if (character.DamageModifier is { } damageDie)
            fields.Add(new EmbedField("Damage", damageDie, IsInline: true));

        if (character.Abilities is { Count: > 0 } abilities)
            fields.Add(new EmbedField("Starting Moves", Truncate(string.Join(", ", abilities.Select(a => a.Name)))));

        if (character.Inventory.Items is { Count: > 0 } items)
            fields.Add(new EmbedField("Inventory", Truncate(string.Join('\n', items.Select(CreationView.ItemLine)))));

        return fields;
    }

    private static string Truncate(string value) =>
        value.Length <= MaxFieldValue ? value : value[..(MaxFieldValue - 1)] + "…";
}
