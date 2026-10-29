using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;

namespace DungeonWorldBot.Features.Characters.Creation.Steps;

/// <summary>
/// Asks for a stat value, which must be one of the values not yet assigned.
/// </summary>
public class StatStep(
    FeedbackService feedbackService,
    string content,
    IReadOnlyCollection<int> available)
    : DialogStep<int?>(feedbackService, content)
{
    private readonly IReadOnlyCollection<int> _available = available;

    protected override Embed BuildPrompt() => new()
    {
        Title = "Character Creation",
        Description = $"{Content}\n{string.Join(", ", _available)}",
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

        if (!int.TryParse(input, out var stat))
        {
            problem = "Your input is not a whole number.";
            return false;
        }

        if (!_available.Contains(stat))
        {
            problem = "Your input is not one of the available options for stats.";
            return false;
        }

        value = stat;
        return true;
    }
}
