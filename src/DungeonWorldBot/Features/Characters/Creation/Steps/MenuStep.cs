using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;

namespace DungeonWorldBot.Features.Characters.Creation.Steps;

/// <summary>
/// Shows a numbered list of options and returns the zero-based index of the one the user picked.
/// </summary>
public class MenuStep : DialogueStep<int?>
{
    protected readonly IReadOnlyList<string> Options;

    public MenuStep(FeedbackService feedbackService, string content, IReadOnlyList<string> options)
        : base(feedbackService, content)
    {
        Options = options;
    }

    public static string FormatOptions(IReadOnlyList<string> options)
    {
        var text = new StringBuilder();
        for (var i = 0; i < options.Count; i++)
        {
            text.AppendLine($"{i + 1}. {options[i]}");
        }

        text.Append("Please select one of these by entering a number.");
        return text.ToString();
    }

    protected override Embed BuildPrompt() => new()
    {
        Title = "Character Creation",
        Description = $"{Content}\n{FormatOptions(Options)}",
        Colour = Color.Yellow,
        Fields = new List<IEmbedField>
        {
            new EmbedField("To Cancel the Character Creation", CancelHint),
        },
    };

    protected override bool TryParse(string input, [NotNullWhen(true)] out int? value, [NotNullWhen(false)] out string? problem)
    {
        value = null;
        problem = null;

        if (!int.TryParse(input, out var choice))
        {
            problem = "Your input is not a whole number.";
            return false;
        }

        if (choice < 1 || choice > Options.Count)
        {
            problem = $"Please enter a number from 1 to {Options.Count}.";
            return false;
        }

        value = choice - 1;
        return true;
    }
}
