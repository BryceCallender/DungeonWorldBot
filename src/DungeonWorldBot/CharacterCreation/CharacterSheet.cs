using DungeonWorldBot.Combat;
using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;

namespace DungeonWorldBot.CharacterCreation;

public static class CharacterSheet
{
    /// <summary>
    /// The fields shown for a newly created character, both while reviewing it and once it's saved.
    /// </summary>
    public static List<IEmbedField> NewCharacterFields(Character character)
    {
        var fields = new List<IEmbedField>
        {
            new EmbedField("Race", character.Race.ToString(), IsInline: false),
        };

        fields.AddRange(character.Stats.Select(s => new EmbedField(s.StatType.ToString(), s.Value.ToString(), IsInline: true)));

        fields.Add(new EmbedField("Health", character.Health.ToDisplay(), IsInline: false));
        fields.Add(new EmbedField("Alignment", character.Alignment.ToString()));
        fields.Add(new EmbedField("Armor", DamageRules.Summary(character)));
        if (character.Abilities is { Count: > 0 } abilities)
        {
            var moves = string.Join(", ", abilities.Select(a => a.Name));
            fields.Add(new EmbedField("Starting Moves", moves.Length <= 1024 ? moves : moves[..1023] + "…"));
        }

        fields.Add(new EmbedField("Inventory", "--------------"));
        fields.AddRange(character.Inventory.Items!.Select(i => new EmbedField(i.Name, i.Amount.ToString(), IsInline: true)));

        return fields;
    }
}
