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
    private enum Section { Name, Race, Class, Stats, Alignment, Gear, ClassChoices, Done }

    private static readonly IReadOnlyList<(string Label, Section Section)> EditOptions =
    [
        ("Edit the Name", Section.Name),
        ("Edit the Race", Section.Race),
        ("Edit the Class", Section.Class),
        ("Edit the Stats", Section.Stats),
        ("Edit the Alignment", Section.Alignment),
        ("Edit the Starting Items", Section.Gear),
        ("Edit the Class Choices", Section.ClassChoices),
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
            // Changing class changes the available gear and class choices, so they're always re-picked with it
            Section.Class => await AskClassAsync(draft) && await AskGearAsync(draft) && await AskClassChoicesAsync(draft),
            Section.Stats => await AskStatsAsync(draft),
            Section.Alignment => await AskAlignmentAsync(draft),
            Section.Gear => await AskGearAsync(draft),
            Section.ClassChoices => await AskClassChoicesAsync(draft),
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
                var picks = await AskPicksAsync(question.Prompt, question.Choices, c => c.Label, question.Picks);
                if (picks is null)
                    return false;

                foreach (var choice in picks)
                {
                    draft.ApplyChoice(choice, dialogue._dice);
                }
            }

            return true;
        }

        private async Task<bool> AskClassChoicesAsync(CharacterDraft draft)
        {
            foreach (var feature in draft.Sheet?.Features ?? [])
            {
                // Options that need another move first (like ship upgrades from Hell and Thunder) aren't offered yet
                // Earlier picks can allow more here, like a Survivor's God cataclysm granting an extra Eternal Mark
                var chosen = draft.StartingMoves().Select(m => m.Name).ToList();
                var picks = await AskPicksAsync(feature.Prompt, feature.Options.Where(o => o.Requires is null).ToList(), o => o.Name, feature.AllowedPicks(chosen));
                if (picks is null)
                    return false;

                draft.FeatureChoices[feature.Name] = picks;
            }

            return true;
        }

        /// <summary>
        /// Asks for <paramref name="count"/> different options, one menu at a time.
        /// </summary>
        /// <returns>The picks, or null if the player cancelled.</returns>
        private async Task<List<T>?> AskPicksAsync<T>(string prompt, IReadOnlyList<T> options, Func<T, string> label, int count)
        {
            var remaining = options.ToList();
            var picks = new List<T>();

            for (var i = 0; i < count && remaining.Count > 0; i++)
            {
                var step = count > 1 ? $"{prompt} (pick {i + 1} of {count})" : prompt;
                var result = await RunAsync(new MenuStep(Feedback, step, remaining.Select(label).ToList()));
                if (result.IsCancelled)
                    return null;

                picks.Add(remaining[result.Value!.Value]);
                remaining.RemoveAt(result.Value!.Value);
            }

            return picks;
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
