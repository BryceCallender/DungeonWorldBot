using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;

namespace DungeonWorldBot.CharacterCreation.Steps;

/// <summary>
/// Shows the character so far and asks what, if anything, to edit.
/// </summary>
public class ReviewStep : MenuStep
{
    private readonly Character _preview;

    public ReviewStep(FeedbackService feedbackService, Character preview, IReadOnlyList<string> options)
        : base(feedbackService, "Would you like to edit any of this information?", options)
    {
        _preview = preview;
    }

    protected override Embed BuildPrompt()
    {
        var fields = new List<IEmbedField>
        {
            new EmbedField("Name", _preview.Name, IsInline: false),
            new EmbedField("Class", $"{_preview.Class.Type} Level: 1", IsInline: false),
        };

        fields.AddRange(CharacterSheet.NewCharacterFields(_preview));
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
