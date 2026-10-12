using DungeonWorldBot.Rolling;
using DungeonWorldBot.CharacterCreation.Steps;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Services.Interactivity;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;

namespace DungeonWorldBot.CharacterCreation;

/// <summary>
/// Walks a player through creating a character over DMs.
/// </summary>
public class CharacterCreationDialogue
{
    private enum Section { Name, Race, Class, Stats, Alignment, Gear, Done }

    private static readonly IReadOnlyList<(string Label, Section Section)> EditOptions =
    [
        ("Edit the Name", Section.Name),
        ("Edit the Race", Section.Race),
        ("Edit the Class", Section.Class),
        ("Edit the Stats", Section.Stats),
        ("Edit the Alignment", Section.Alignment),
        ("Edit the Starting Items", Section.Gear),
        ("No Editing", Section.Done),
    ];

    private readonly FeedbackService _feedbackService;
    private readonly InteractivityService _interactivity;
    private readonly IDiscordRestUserAPI _userAPI;
    private readonly IDiceRoller _dice;

    public CharacterCreationDialogue(
        FeedbackService feedbackService,
        InteractivityService interactivity,
        IDiscordRestUserAPI userAPI,
        IDiceRoller dice)
    {
        _feedbackService = feedbackService;
        _interactivity = interactivity;
        _userAPI = userAPI;
        _dice = dice;
    }

    /// <returns>The finished (unsaved) character, or null if the player cancelled or stopped responding.</returns>
    public async Task<Character?> RunAsync(Snowflake userId, CancellationToken ct = default)
    {
        var getDM = await _userAPI.CreateDMAsync(userId, ct);
        if (!getDM.IsSuccess)
            return null;

        var session = new Session(this, getDM.Entity.ID, userId, ct);
        var draft = new CharacterDraft();

        Section[] firstPass = [Section.Name, Section.Race, Section.Class, Section.Stats, Section.Alignment];
        foreach (var section in firstPass)
        {
            if (!await session.AskAsync(section, draft))
                return null;
        }

        while (true)
        {
            var preview = draft.Build(userId);
            var review = await session.RunAsync(new ReviewStep(_feedbackService, preview, EditOptions.Select(o => o.Label).ToList()));
            if (review.IsCancelled)
                return null;

            var section = EditOptions[review.Value!.Value].Section;
            if (section == Section.Done)
                return preview;

            if (!await session.AskAsync(section, draft))
                return null;
        }
    }

    private sealed class Session(CharacterCreationDialogue dialogue, Snowflake channelId, Snowflake userId, CancellationToken ct)
    {
        private FeedbackService Feedback => dialogue._feedbackService;

        public Task<StepResult<T>> RunAsync<T>(DialogueStep<T> step) =>
            step.RunAsync(dialogue._interactivity, channelId, userId, ct);

        /// <returns>False if the player cancelled.</returns>
        public async Task<bool> AskAsync(Section section, CharacterDraft draft) => section switch
        {
            Section.Name => await AskNameAsync(draft),
            Section.Race => await AskRaceAsync(draft),
            // Changing class changes the available gear, so gear is always re-picked with it
            Section.Class => await AskClassAsync(draft) && await AskGearAsync(draft),
            Section.Stats => await AskStatsAsync(draft),
            Section.Alignment => await AskAlignmentAsync(draft),
            Section.Gear => await AskGearAsync(draft),
            _ => true,
        };

        private async Task<bool> AskNameAsync(CharacterDraft draft)
        {
            var result = await RunAsync(new TextStep(Feedback, "What is your characters Name?", 2, 100));
            if (result.IsCancelled)
                return false;

            draft.Name = result.Value!;
            return true;
        }

        private async Task<bool> AskRaceAsync(CharacterDraft draft)
        {
            var options = CharacterDraft.RaceOptions;
            var result = await RunAsync(new MenuStep(Feedback, "What is your characters Race?", options.Select(o => o.Label).ToList()));
            if (result.IsCancelled)
                return false;

            draft.Race = options[result.Value!.Value].Value;
            return true;
        }

        private async Task<bool> AskClassAsync(CharacterDraft draft)
        {
            var kits = ClassKits.All;
            var result = await RunAsync(new MenuStep(Feedback, "What is your characters Class?", kits.Select(k => k.DisplayName).ToList()));
            if (result.IsCancelled)
                return false;

            draft.SetClass(kits[result.Value!.Value], dialogue._dice);
            return true;
        }

        private async Task<bool> AskStatsAsync(CharacterDraft draft)
        {
            var available = CharacterDraft.StatArray.ToList();
            var stats = new Dictionary<StatType, int>();

            foreach (var statType in CharacterDraft.StatOrder)
            {
                var result = await RunAsync(new StatStep(Feedback, $"Which value would you like your {StatName(statType)} stat to be?", available));
                if (result.IsCancelled)
                    return false;

                var value = result.Value!.Value;
                available.Remove(value);
                stats[statType] = value;
            }

            // Only replace the stats once all six have been picked, so a half-finished edit can't leave gaps
            draft.Stats.Clear();
            foreach (var (type, value) in stats)
            {
                draft.Stats[type] = value;
            }

            return true;
        }

        private async Task<bool> AskAlignmentAsync(CharacterDraft draft)
        {
            var options = CharacterDraft.AlignmentOptions;
            var result = await RunAsync(new MenuStep(Feedback, "What is your characters Alignment?", options.Select(o => o.Label).ToList()));
            if (result.IsCancelled)
                return false;

            draft.Alignment = options[result.Value!.Value].Value;
            return true;
        }

        private async Task<bool> AskGearAsync(CharacterDraft draft)
        {
            draft.ResetGear(dialogue._dice);

            foreach (var question in draft.Kit.Questions)
            {
                var result = await RunAsync(new MenuStep(Feedback, question.Prompt, question.Choices.Select(c => c.Label).ToList()));
                if (result.IsCancelled)
                    return false;

                draft.ApplyChoice(question.Choices[result.Value!.Value], dialogue._dice);
            }

            return true;
        }

        private static string StatName(StatType type) => type switch
        {
            StatType.Str => "Strength",
            StatType.Dex => "Dexterity",
            StatType.Con => "Constitution",
            StatType.Int => "Intelligence",
            StatType.Wis => "Wisdom",
            StatType.Cha => "Charisma",
            _ => type.ToString(),
        };
    }
}
