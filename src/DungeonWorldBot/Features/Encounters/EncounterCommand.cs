using System.ComponentModel;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// The fight as the players see it. The DM runs it with /dm encounter.
/// </summary>
[Group("encounter")]
public class EncounterCommand : DungeonWorldCommandBase
{
    private readonly IEncounterService _encounterService;

    public EncounterCommand(ICommandContext context, FeedbackService feedbackService, IEncounterService encounterService)
        : base(context, feedbackService)
    {
        _encounterService = encounterService;
    }

    [Command("show")]
    [Description("See who you're fighting and how hurt they look")]
    public async Task<IResult> ShowAsync()
    {
        if (await _encounterService.GetActiveAsync() is not { } encounter)
            return await ReplyWithErrorAsync("No fight is going on.");

        return await ReplyAsync($"⚔️ {encounter.Name}", EnemySheet.PlayerList(encounter));
    }
}
