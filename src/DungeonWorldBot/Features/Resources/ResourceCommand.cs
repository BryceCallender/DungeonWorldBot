using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Resources;

[Group("resource")]
public class ResourceCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    IResourceService resourceService)
    : DungeonWorldCommandBase(context, feedbackService)
{
    private const string NameDescription = "The resource, e.g. Gambit, Control, Power, Thirst or Wrath";

    [Command("show")]
    [Description("Show a character's class resources")]
    public async Task<IResult> ShowAsync(
        [Description("The user to see the resources of. Don't specify a user to see your own.")]
        IUser? user = null)
    {
        var character = user is not null
            ? await characterService.GetCharacterFromUserAsync(user.ID)
            : await GetOwnCharacterAsync();

        if (character is null)
        {
            return await ReplyWithErrorAsync(user is null
                ? NoCharacterMessage
                : $"{user.Username} does not have a character.");
        }

        var resources = ClassSheetLibrary.ResourcesOf(character);
        if (resources.Count == 0)
            return await ReplyWithErrorAsync($"{character.Name} doesn't have any class resources to track.");

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: $"{character.Name}'s Resources",
                Fields: resources.Select(r => (IEmbedField)new EmbedField($"{r.Name}: {Format(character, r)}", r.Summary)).ToList(),
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("gain")]
    [Description("Gain points of a class resource")]
    public async Task<IResult> GainAsync(
        [Description(NameDescription)]
        [AutocompleteProvider(ClassResourceAutocomplete.ID)]
        string name,
        [Description("How many to gain")]
        [MinValue(1)]
        int amount = 1)
    {
        var (character, resource, error) = await FindAsync(name);
        if (error is not null)
            return error;

        var before = resourceService.GetValue(character!, resource!);
        var after = await resourceService.SetValueAsync(character!, resource!, before + amount);

        var description = $"{character!.Name} gained {after - before} {resource!.Name} ({Format(character, resource)})";
        if (after - before < amount)
            description += $"\n{resource.Name} can't go above {resourceService.GetMax(character, resource)}.";

        if (after > before && resource.GainNote is not null)
            description += $"\n\n{resource.GainNote}";

        return await ReplyAsync($"+{after - before} {resource.Name}", description);
    }

    [Command("spend")]
    [Description("Spend points of a class resource")]
    public async Task<IResult> SpendAsync(
        [Description(NameDescription)]
        [AutocompleteProvider(ClassResourceAutocomplete.ID)]
        string name,
        [Description("How many to spend")]
        [MinValue(1)]
        int amount = 1)
    {
        var (character, resource, error) = await FindAsync(name);
        if (error is not null)
            return error;

        var current = resourceService.GetValue(character!, resource!);
        if (current < amount)
            return await ReplyWithErrorAsync($"{character!.Name} only has {current} {resource!.Name}.");

        await resourceService.SetValueAsync(character!, resource!, current - amount);

        return await ReplyAsync($"-{amount} {resource!.Name}", $"{character!.Name} spent {amount} {resource.Name} ({Format(character, resource)})");
    }

    [Command("set")]
    [Description("Set a class resource to an exact value")]
    public async Task<IResult> SetAsync(
        [Description(NameDescription)]
        [AutocompleteProvider(ClassResourceAutocomplete.ID)]
        string name,
        [Description("The new value")]
        [MinValue(0)]
        int value)
    {
        var (character, resource, error) = await FindAsync(name);
        if (error is not null)
            return error;

        var before = resourceService.GetValue(character!, resource!);
        var after = await resourceService.SetValueAsync(character!, resource!, value);

        var description = $"{character!.Name}'s {resource!.Name} is now {Format(character, resource)}";
        if (after > before && resource.GainNote is not null)
            description += $"\n\n{resource.GainNote}";
        else if (after < before && resource.ResetNote is not null)
            description += $"\n\n{resource.ResetNote}";

        return await ReplyAsync($"{resource.Name} Set", description);
    }

    [Command("reset")]
    [Description("Reset class resources, e.g. Gambits after a battle. Leave blank to reset all of them")]
    public async Task<IResult> ResetAsync(
        [Description("The resource to reset. Leave blank to reset all of them")]
        [AutocompleteProvider(ClassResourceAutocomplete.ID)]
        string? name = null)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        List<ClassResource> toReset;
        if (string.IsNullOrWhiteSpace(name))
        {
            toReset = ClassSheetLibrary.ResourcesOf(character).Where(r => r.ResetWithAll).ToList();
            if (toReset.Count == 0)
                return await ReplyWithErrorAsync($"{character.Name} doesn't have any class resources to reset.");
        }
        else
        {
            var (_, resource, error) = await FindAsync(name, character);
            if (error is not null)
                return error;
            toReset = [resource!];
        }

        foreach (var resource in toReset)
        {
            await resourceService.SetValueAsync(character, resource, resourceService.GetResetValue(character, resource));
        }

        var notes = toReset.Where(r => r.ResetNote is not null).Select(r => r.ResetNote!).ToList();
        var summary = string.Join('\n', toReset.Select(r => $"**{r.Name}:** {Format(character, r)}"));

        return await ReplyAsync("Resources Reset", notes.Count > 0 ? $"{summary}\n\n{string.Join('\n', notes)}" : summary);
    }

    private string Format(Character character, ClassResource resource) => resourceService.Format(character, resource);

    /// <returns>The character and resource, or an error that has already been sent to the player.</returns>
    private async Task<(Character? Character, ClassResource? Resource, IResult? Error)> FindAsync(string name, Character? character = null)
    {
        character ??= await GetOwnCharacterAsync();
        if (character is null)
            return (null, null, await ReplyWithErrorAsync(NoCharacterMessage));

        var sheet = ClassSheetLibrary.For(character.Class.Type);
        if (ClassSheetLibrary.CommonResources.FirstOrDefault(r => r.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)) is { } common)
            return (character, common, null);

        var resource = sheet?.FindResource(name);
        if (resource is null)
            return (character, null, await ReplyWithErrorAsync($"The {character.Class.Type} doesn't have a resource called {name}."));

        if (!sheet!.ResourcesFor(ClassSheetLibrary.KnownMoves(character)).Contains(resource))
            return (character, null, await ReplyWithErrorAsync($"You need {resource.GrantedBy} before you have {resource.Name}."));

        return (character, resource, null);
    }

    private async Task<Character?> GetOwnCharacterAsync() =>
        await characterService.GetCharacterFromUserAsync(GetUserId());
}
