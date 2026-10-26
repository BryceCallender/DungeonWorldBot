using System.ComponentModel;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
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
    private readonly ISpotlightService _spotlightService;
    private readonly ICharacterService _characterService;

    public EncounterCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        IEncounterService encounterService,
        ISpotlightService spotlightService,
        ICharacterService characterService)
        : base(context, feedbackService)
    {
        _encounterService = encounterService;
        _spotlightService = spotlightService;
        _characterService = characterService;
    }

    private const string NoFight = "No fight is going on.";
    private const string FightDescription = "Which fight, when there's more than one going on. Yours, if you leave it blank";

    [Command("show")]
    [Description("See who you're fighting and how hurt they look")]
    public async Task<IResult> ShowAsync(
        [Description(FightDescription)]
        [AutocompleteProvider(FightAutocomplete.ID)]
        string? fight = null)
    {
        var picked = await PickAsync(fight);
        if (picked.Encounter is not { } encounter)
            return await ReplyWithErrorAsync(picked.Error!);

        return await ReplyAsync($"⚔️ {encounter.Name}", EnemySheet.PlayerList(encounter));
    }

    [Command("spotlight")]
    [Description("See who's had the spotlight in the fight, what they did last, and who's due a turn")]
    public async Task<IResult> SpotlightAsync(
        [Description(FightDescription)]
        [AutocompleteProvider(FightAutocomplete.ID)]
        string? fight = null)
    {
        var picked = await PickAsync(fight);
        if (picked.Encounter is not { } encounter)
            return await ReplyWithErrorAsync(picked.Error!);

        var spotlight = await _spotlightService.GetAsync(encounter, await _characterService.GetCharactersAsync());

        var fields = new List<IEmbedField>();
        if (spotlight.Party.Count > 0)
            fields.Add(new EmbedField("Party", Truncate(string.Join('\n', spotlight.Party.Select(Line)), 1024)));
        if (spotlight.Allies.Count > 0)
            fields.Add(new EmbedField("Allies", Truncate(string.Join('\n', spotlight.Allies.Select(Line)), 1024)));
        if (spotlight.Enemies.Count > 0)
            fields.Add(new EmbedField("Enemies", Truncate(string.Join('\n', spotlight.Enemies.Select(Line)), 1024)));

        var description = spotlight.Misses > 0
            ? $"{spotlight.Rolls} rolls, {spotlight.Misses} misses · 💤 is due the spotlight"
            : "💤 is due the spotlight";
        return await ReplyAsync($"🔦 {encounter.Name}", fields.Count > 0 ? description : "Nobody's in the fight yet.", fields);
    }

    /// <summary>
    /// The fight named, or else the one the player's character is in, or else the only one going on.
    /// </summary>
    private async Task<FightChoice> PickAsync(string? fight)
    {
        var yours = string.IsNullOrWhiteSpace(fight) && await _characterService.GetCharacterFromUserAsync(GetUserId()) is { } character
            ? await _encounterService.GetForAsync(character.ID)
            : null;
        return await FightChoice.PickAsync(_encounterService, fight, NoFight, yours);
    }

    /// <summary>
    /// e.g. "💤 **Aria** · 2× · Volley · 11 · Strong Hit (10+) → Goblin 2 · 💥 4 damage".
    /// </summary>
    private static string Line(SpotlightLine line) =>
        (line.Quiet ? "💤 " : string.Empty)
        + $"**{line.Name}**"
        + (line.Last is { } last ? $" · {line.Actions}× · {last.Summary}" : " · hasn't acted");
}
