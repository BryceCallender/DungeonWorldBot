using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace DungeonWorldBot.Infrastructure;

/// <summary>
/// Discord rejects a whole message when one part of an embed is too long, so long notes or lists are cut short instead.
/// </summary>
public static class EmbedLimits
{
    public const int MaxFields = 25;

    private const int TitleLength = 256;
    private const int DescriptionLength = 4096;
    private const int FieldNameLength = 256;
    private const int FieldValueLength = 1024;

    /// <summary>
    /// The embed cut down to what Discord takes.
    /// </summary>
    public static Embed Fit(Embed embed) => embed with
    {
        Title = Fit(embed.Title, TitleLength),
        Description = Fit(embed.Description, DescriptionLength),
        Fields = embed.Fields.HasValue
            ? new(embed.Fields.Value.Take(MaxFields).Select(Fit).ToList())
            : embed.Fields,
    };

    public static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..(length - 1)] + "…";

    private static IEmbedField Fit(IEmbedField field) =>
        new EmbedField(Truncate(field.Name, FieldNameLength), Truncate(field.Value, FieldValueLength), field.IsInline);

    // Discord rejects an empty description too, so it's left off
    private static Optional<string> Fit(Optional<string> text, int length) =>
        text.HasValue && !string.IsNullOrEmpty(text.Value) ? Truncate(text.Value, length) : default(Optional<string>);
}
