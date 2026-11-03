using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Rolls;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Interactivity;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Features.Characters.Creation;

/// <summary>
/// The menus and buttons of character creation, started by /character create. Each carries the screen it was drawn for
/// as its state, so one left on an old message can't answer a different question.
/// </summary>
public class CharacterCreationInteractions(
    IInteractionContext context,
    IDiscordRestInteractionAPI interactionAPI,
    CreationSessions sessions,
    ICharacterService characterService,
    IDiceRoller dice)
    : InteractionGroup
{
    public const string Class = "creation-class";
    public const string Name = "creation-name";
    public const string Race = "creation-race";
    public const string Alignment = "creation-alignment";
    public const string Stat = "creation-stat";
    public const string ResetStats = "creation-reset-stats";
    public const string Gear = "creation-gear";
    public const string Choice = "creation-choice";
    public const string Edit = "creation-edit";
    public const string Back = "creation-back";
    public const string Next = "creation-next";
    public const string Create = "creation-create";
    public const string Cancel = "creation-cancel";

    // The form's text box, bound to the parameter of the same name
    private const string NameInput = "name";
    private const int MinNameLength = 2;
    private const int MaxNameLength = 100;

    private const string ExpiredMessage = "This character draft has expired. Run `/character create` to start a new one.";

    // Every handler answers by editing the creation message itself, so Remora mustn't answer for us
    [SelectMenu(Class)]
    [SuppressInteractionResponse(true)]
    public Task<Result> ClassAsync(string state, IReadOnlyList<string> values) =>
        AnswerAsync(state, session => Pick(values, ClassKits.All, kits => session.ChooseClass(kits[0], dice)));

    [SelectMenu(Race)]
    [SuppressInteractionResponse(true)]
    public Task<Result> RaceAsync(string state, IReadOnlyList<string> values) =>
        AnswerAsync(state, session => Pick(values, CharacterDraft.RaceOptions, races => session.Draft.Race = races[0].Value));

    [SelectMenu(Alignment)]
    [SuppressInteractionResponse(true)]
    public Task<Result> AlignmentAsync(string state, IReadOnlyList<string> values) =>
        AnswerAsync(state, session => Pick(values, CharacterDraft.AlignmentOptions, alignments => session.Draft.Alignment = alignments[0].Value));

    [SelectMenu(Stat)]
    [SuppressInteractionResponse(true)]
    public Task<Result> StatAsync(string state, IReadOnlyList<string> values) =>
        AnswerAsync(state, session => AssignStat(session.Draft, values));

    [Button(ResetStats)]
    [SuppressInteractionResponse(true)]
    public Task<Result> ResetStatsAsync(string state) =>
        NavigateAsync(state, session => session.Draft.Stats.Clear());

    [SelectMenu(Gear)]
    [SuppressInteractionResponse(true)]
    public Task<Result> GearAsync(string state, IReadOnlyList<string> values) =>
        AnswerAsync(state, session =>
        {
            var question = session.Current!.Question;
            return Pick(values, session.Draft.Kit.Questions[question].Choices, choices => session.Draft.ChooseGear(question, choices, dice));
        });

    [SelectMenu(Choice)]
    [SuppressInteractionResponse(true)]
    public Task<Result> ClassChoiceAsync(string state, IReadOnlyList<string> values) =>
        AnswerAsync(state, session =>
        {
            var feature = CreationSession.ClassChoices(session.Draft)[session.Current!.Question];
            return Pick(values, CharacterDraft.PickableOptions(feature), moves => session.Draft.FeatureChoices[feature.Name] = moves.ToList());
        });

    [SelectMenu(Edit)]
    [SuppressInteractionResponse(true)]
    public Task<Result> EditAsync(string state, IReadOnlyList<string> values) =>
        NavigateAsync(state, session =>
        {
            if (values.Count == 1 && Enum.TryParse<CreationSection>(values[0], out var section))
                session.Edit(section);
        });

    [Button(Back)]
    [SuppressInteractionResponse(true)]
    public Task<Result> BackAsync(string state) => NavigateAsync(state, session => session.Back());

    [Button(Next)]
    [SuppressInteractionResponse(true)]
    public Task<Result> NextAsync(string state) => AnswerAsync(state, _ => null);

    // A form has to be the interaction's first response
    [Button(Name)]
    [SuppressInteractionResponse(true)]
    public async Task<Result> AskNameAsync(string state)
    {
        var current = sessions.Find(UserId)?.Draft.Name;
        var form = new InteractionModalCallbackData(
            CustomIDHelpers.CreateModalIDWithState(Name, state),
            "Name your character",
            [
                new ActionRowComponent(
                [
                    new TextInputComponent(
                        NameInput,
                        TextInputStyle.Short,
                        "Name",
                        MinLength: MinNameLength,
                        MaxLength: MaxNameLength,
                        IsRequired: true,
                        Value: string.IsNullOrEmpty(current) ? default(Optional<string>) : current,
                        Placeholder: default),
                ]),
            ]);

        return await interactionAPI.CreateInteractionResponseAsync(
            context.Interaction.ID,
            context.Interaction.Token,
            new InteractionResponse(InteractionCallbackType.Modal, new(form)),
            ct: CancellationToken);
    }

    [Modal(Name)]
    [SuppressInteractionResponse(true)]
    public async Task<Result> NameAsync(string state, string name)
    {
        var trimmed = name.Trim();
        var problem = await NameProblemAsync(trimmed);

        return await AnswerAsync(state, session =>
        {
            if (problem is null)
                session.Draft.Name = trimmed;
            return problem;
        });
    }

    [Button(Create)]
    [SuppressInteractionResponse(true)]
    public async Task<Result> CreateAsync(string state)
    {
        var session = sessions.Find(UserId);
        if (session is null)
            return await ShowAsync(CreationView.Ended(ExpiredMessage));

        if (state != session.State)
            return await ShowAsync(CreationView.Render(session, UserId, "That button was from an earlier screen."));

        if (await characterService.GetCharacterFromUserAsync(UserId) is not null)
        {
            sessions.End(UserId);
            return await ShowAsync(CreationView.Ended("You already have a character. See them with `/character profile`."));
        }

        if (await NameProblemAsync(session.Draft.Name) is { } problem)
        {
            session.Edit(CreationSection.Name);
            return await ShowAsync(CreationView.Render(session, UserId, problem));
        }

        // Only the click that ends the session saves, so pressing Create twice can't make two characters
        if (!sessions.End(UserId))
            return Result.FromSuccess();

        var character = session.Draft.Build(UserId);
        await characterService.AddCharacterAsync(character);

        return await ShowAsync(CreationView.Created(character));
    }

    [Button(Cancel)]
    [SuppressInteractionResponse(true)]
    public async Task<Result> CancelAsync(string state)
    {
        sessions.End(UserId);
        return await ShowAsync(CreationView.Ended("Character creation canceled. Run `/character create` to start again."));
    }

    private Snowflake UserId =>
        context.TryGetUserID(out var userId) ? userId : throw new NotSupportedException();

    /// <summary>
    /// Applies an answer to the current screen and moves on once the screen is fully answered.
    /// </summary>
    /// <param name="answer">Takes the answer, returning why it can't if it can't.</param>
    private Task<Result> AnswerAsync(string state, Func<CreationSession, string?> answer) =>
        UpdateAsync(state, session =>
        {
            var problem = answer(session);
            if (problem is null && session.IsAnswered())
                session.Next();
            return problem;
        });

    /// <summary>
    /// Moves around without answering anything.
    /// </summary>
    private Task<Result> NavigateAsync(string state, Action<CreationSession> move) =>
        UpdateAsync(state, session =>
        {
            move(session);
            return null;
        });

    private async Task<Result> UpdateAsync(string state, Func<CreationSession, string?> change)
    {
        var session = sessions.Find(UserId);
        if (session is null)
            return await ShowAsync(CreationView.Ended(ExpiredMessage));

        CreationMessage message;
        lock (session)
        {
            // An old message's menus are drawn for a screen the player has since left
            var problem = state == session.State ? change(session) : "That was from an earlier screen. Here's where you're up to.";
            message = CreationView.Render(session, UserId, problem);
        }

        return await ShowAsync(message);
    }

    private Task<Result> ShowAsync(CreationMessage message) =>
        interactionAPI.CreateInteractionResponseAsync(
            context.Interaction.ID,
            context.Interaction.Token,
            new InteractionResponse(
                InteractionCallbackType.UpdateMessage,
                new(new InteractionMessageCallbackData(Embeds: new IEmbed[] { message.Embed }, Components: new(message.Components)))),
            ct: CancellationToken);

    private async Task<string?> NameProblemAsync(string name)
    {
        if (name.Length < MinNameLength)
            return $"Names need at least {MinNameLength} characters.";

        var names = await characterService.GetCharacterNamesAsync();
        return names.Any(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ? $"There's already a character called {name}. Pick another name."
            : null;
    }

    private static string? AssignStat(CharacterDraft draft, IReadOnlyList<string> values)
    {
        if (values.Count != 1 || !Enum.TryParse<StatType>(values[0], out var stat) || !Enum.IsDefined(stat) || draft.Stats.ContainsKey(stat))
            return "Pick one of the stats that doesn't have a score yet.";

        if (draft.NextStatValue() is not { } value)
            return null;

        draft.AssignStat(stat, value);

        // With one stat left there's only one place the last score can go
        var unassigned = CharacterDraft.StatOrder.Where(s => !draft.Stats.ContainsKey(s)).ToList();
        if (unassigned.Count == 1 && draft.NextStatValue() is { } last)
            draft.AssignStat(unassigned[0], last);

        return null;
    }

    /// <summary>
    /// Turns a menu's picked values (positions in <paramref name="options"/>) back into the options.
    /// </summary>
    private static string? Pick<T>(IReadOnlyList<string> values, IReadOnlyList<T> options, Action<IReadOnlyList<T>> apply)
    {
        var indices = values.Select(v => int.TryParse(v, out var i) ? i : -1).ToList();
        if (indices.Count == 0 || indices.Any(i => i < 0 || i >= options.Count))
            return "That option isn't available any more. Pick again.";

        apply(indices.Distinct().Select(i => options[i]).ToList());
        return null;
    }
}
