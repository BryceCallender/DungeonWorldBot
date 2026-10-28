using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;

namespace DungeonWorldBot.Features.Characters.Creation.Steps;

/// <summary>
/// Shows the character so far and asks what, if anything, to edit.
/// </summary>
public class ReviewStep(
    FeedbackService feedbackService,
    Character preview,
    IReadOnlyList<string> options)
    : MenuStep(feedbackService, "Would you like to edit any of this information?", options)
{
    protected override Embed BuildPrompt()
    {
        List<IEmbedField> fields =
        [
            new EmbedField("Name", preview.Name, IsInline: false),
            new EmbedField("Class", $"{preview.Class.Type} Level: 1", IsInline: false),
        ];

        fields.AddRange(CharacterSheet.NewCharacterFields(preview));
        fields.Add(new EmbedField(Content, FormatOptions(Options)));
        fields.Add(new EmbedField("To Cancel the Character Creation", CancelHint));

        return new Embed
        {
            Title = "Character Creation",
            Type = EmbedType.Rich,
            Fields = fields,
            Colour = FeedbackService.Theme.Primary,
        };
    }
}
