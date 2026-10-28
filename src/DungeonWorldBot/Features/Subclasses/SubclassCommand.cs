using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Subclasses;

[Group("subclass")]
public class SubclassCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    ISubclassService subclassService,
    IGameMasterService gameMaster)
    : DungeonWorldCommandBase(context, feedbackService)
{
    [Command("list")]
    [Description("List every subclass and which classes can take it")]
    public async Task<IResult> ListAsync()
    {
        var fields = SubclassLibrary.All
            .Select(s => (IEmbedField)new EmbedField(
                $"{s.Name} ({(s.Classes.Count > 0 ? string.Join(", ", s.Classes) : "any class")})",
                Truncate($"*{s.Title}.* {s.Summary}\n{s.Source}", 1024)))
            .ToList();

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "Subclasses",
                Description: "Read one with /subclass info, and join with /subclass join",
                Fields: fields,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("info")]
    [Description("Read a subclass's moves, or see your own subclass")]
    public async Task<IResult> InfoAsync(
        [Description("The subclass to read. Leave blank to see your own.")]
        [AutocompleteProvider(SubclassAutocomplete.ID)]
        string? subclass = null,
        [Description("See another player's subclass")]
        IUser? user = null)
    {
        Character? character = null;
        Subclass? found;
        if (subclass is not null)
        {
            found = SubclassLibrary.Find(subclass);
            if (found is null)
                return await ReplyWithErrorAsync($"There's no subclass called {subclass}. See /subclass list");
        }
        else
        {
            character = await GetCharacterAsync(user);
            if (character is null)
                return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");

            found = SubclassLibrary.Of(character);
            if (found is null)
                return await ReplyWithErrorAsync($"{character.Name} doesn't have a subclass. See /subclass list");
        }

        var fields = found.Moves
            .Select(m =>
            {
                var unlocked = character is not null && !m.Hidden && m.Level <= character.Level;
                var heading = m.Hidden ? $"Level {m.Level}: ???" : $"Level {m.Level}: {m.Move.Name}";
                if (character is not null)
                    heading = (unlocked ? "✅ " : "🔒 ") + heading;

                return (IEmbedField)new EmbedField(Truncate(heading, 256), Truncate(m.Move.Summary, 1024));
            })
            .ToList();

        if (character is not null && found.Status?.Invoke(character) is { } status)
            fields.Insert(0, new EmbedField("Status", Truncate(status, 1024)));

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: character is null ? found.Name : $"{character.Name}: {found.Name}",
                Description: $"*{found.Title}*\n{found.Summary}\n\n{found.Source} subclass for {(found.Classes.Count > 0 ? string.Join(", ", found.Classes) : "any class")}." +
                             (found.CommandHint is not null ? $"\n{found.CommandHint}" : string.Empty),
                Fields: fields,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("join")]
    [Description("Take a subclass. Its moves unlock as you level (the DM can choose for someone else)")]
    public async Task<IResult> JoinAsync(
        [Description("The subclass to take")]
        [AutocompleteProvider(SubclassAutocomplete.ID)]
        string subclass,
        [Description("DM only: the player whose character takes it. Leave blank for your own.")]
        IUser? user = null)
    {
        if (await CheckOthersAsync(user) is { } denied)
            return denied;

        var character = await GetCharacterAsync(user);
        if (character is null)
            return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");

        if (SubclassLibrary.Find(subclass) is not { } found)
            return await ReplyWithErrorAsync($"There's no subclass called {subclass}. See /subclass list");

        if (!found.AllowedFor(character.Class.Type))
            return await ReplyWithErrorAsync($"{found.Name} is for the {string.Join(" or ", found.Classes)}, not the {character.Class.Type}.");

        if (SubclassLibrary.Of(character) is { } current)
        {
            return await ReplyWithErrorAsync(current == found
                ? $"{character.Name} is already a {found.Name}."
                : $"{character.Name} is already a {current.Name}. Leave it first with /subclass leave");
        }

        var learned = await subclassService.JoinAsync(character, found);

        var description = $"{character.Name} is now a **{found.Name}**, *{found.Title}*.";
        if (learned.Count > 0)
            description += $"\n\nLearned {string.Join(", ", learned.Select(m => m.Name))}.";

        var later = found.Moves.Where(m => m.Level > character.Level).ToList();
        if (later.Count > 0)
            description += $"\n\nStill to come: {string.Join(", ", later.Select(m => $"{(m.Hidden ? "???" : m.Move.Name)} (level {m.Level})"))}.";
        if (found.CommandHint is not null)
            description += $"\n\n{found.CommandHint}";

        return await ReplyAsync("Subclass Joined!", description, character);
    }

    [Command("leave")]
    [Description("Give up your subclass and its moves (the DM can do this for someone else)")]
    public async Task<IResult> LeaveAsync(
        [Description("DM only: the player whose character leaves it. Leave blank for your own.")]
        IUser? user = null)
    {
        if (await CheckOthersAsync(user) is { } denied)
            return denied;

        var character = await GetCharacterAsync(user);
        if (character is null)
            return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");

        if (SubclassLibrary.Of(character) is not { } current)
            return await ReplyWithErrorAsync($"{character.Name} doesn't have a subclass.");

        await subclassService.LeaveAsync(character, current);
        return await ReplyAsync("Subclass Left", $"{character.Name} is no longer a {current.Name}, and lost its moves.", character);
    }

    /// <returns>An error if the user is acting on someone else's character without being the DM.</returns>
    private async Task<IResult?> CheckOthersAsync(IUser? user)
    {
        if (user is null || !_context.TryGetUserID(out var self) || user.ID == self || await gameMaster.IsGameMasterAsync(CancellationToken))
            return null;

        return await ReplyWithErrorAsync($"Only someone with the {gameMaster.RoleName} role can change someone else's subclass.");
    }

    private async Task<Character?> GetCharacterAsync(IUser? user)
    {
        if (user is not null)
            return await characterService.GetCharacterFromUserAsync(user.ID);

        var userId = GetUserId();

        return await characterService.GetCharacterFromUserAsync(userId);
    }

    private async Task<IResult> ReplyAsync(string title, string description, Character character)
    {
        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: title,
                Thumbnail: ImageHelper.ClassThumbnail(character.Class.Type),
                Description: Truncate(description, 4096),
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }
}
