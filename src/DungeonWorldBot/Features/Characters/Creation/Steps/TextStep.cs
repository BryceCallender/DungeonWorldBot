using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;

namespace DungeonWorldBot.Features.Characters.Creation.Steps;

public class TextStep(
    FeedbackService feedbackService,
    string content,
    int minLength,
    int maxLength)
    : DialogStep<string>(feedbackService, content)
{
    protected override Embed BuildPrompt() => new()
    {
        Title = "Character Creation",
        Description = Content,
        Colour = Color.Yellow,
        Fields = new List<IEmbedField>
        {
            new EmbedField("To Cancel the Character Creation", CancelHint),
            new EmbedField("Min Length:", $"{minLength} characters"),
            new EmbedField("Max Length:", $"{maxLength} characters"),
        },
    };

    protected override bool TryParse(string input, [NotNullWhen(true)] out string? value, [NotNullWhen(false)] out string? problem)
    {
        value = null;
        problem = null;

        if (input.Length < minLength)
        {
            problem = $"Your input is {minLength - input.Length} characters too short";
            return false;
        }

        if (input.Length > maxLength)
        {
            problem = $"Your input is {input.Length - maxLength} characters too long";
            return false;
        }

        value = input;
        return true;
    }
}
