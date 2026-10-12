using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;

namespace DungeonWorldBot.CharacterCreation.Steps;

public class TextStep : DialogueStep<string>
{
    private readonly int _minLength;
    private readonly int _maxLength;

    public TextStep(FeedbackService feedbackService, string content, int minLength, int maxLength)
        : base(feedbackService, content)
    {
        _minLength = minLength;
        _maxLength = maxLength;
    }

    protected override Embed BuildPrompt() => new()
    {
        Title = "Character Creation",
        Description = Content,
        Colour = Color.Yellow,
        Fields = new List<IEmbedField>
        {
            new EmbedField("To Cancel the Character Creation", CancelHint),
            new EmbedField("Min Length:", $"{_minLength} characters"),
            new EmbedField("Max Length:", $"{_maxLength} characters"),
        },
    };

    protected override bool TryParse(string input, [NotNullWhen(true)] out string? value, [NotNullWhen(false)] out string? problem)
    {
        value = null;
        problem = null;

        if (input.Length < _minLength)
        {
            problem = $"Your input is {_minLength - input.Length} characters too short";
            return false;
        }

        if (input.Length > _maxLength)
        {
            problem = $"Your input is {input.Length - _maxLength} characters too long";
            return false;
        }

        value = input;
        return true;
    }
}
