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
        var character = await characterService.GetCharacterToShowAsync(user?.ID ?? GetUserId());

        if (character is null)
        {
            return await ReplyWithErrorAsync(user is null
                ? NoCharacterMessage
                : $"{user.Username} does not have a character.");
        }

        var resources = ClassSheetLibrary.ResourcesOf(character);
        if (resources.Count == 0)
            return await ReplyWithErrorAsync($"{character.Name} doesn't have any class resources to track.");

        return await ReplyWithEmbedAsync(
            new Embed(
                Title: $"{character.Name}'s Resources",
                Fields: resources.Select(r => (IEmbedField)new EmbedField($"{r.Name}: {Format(character, r)}", r.Summary)).ToList(),
                Colour: _feedbackService.Theme.Primary
            )
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
        var found = await FindAsync(name);
        if (!found.IsDefined(out var own))
            return await ReplyWithErrorAsync(found.Error!.Message);
        var (character, resource) = own;

        var before = resourceService.GetValue(character, resource);
        var after = await resourceService.SetValueAsync(character, resource, before + amount);

        var description = $"{character.Name} gained {after - before} {resource.Name} ({Format(character, resource)})";
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
        var found = await FindAsync(name);
        if (!found.IsDefined(out var own))
            return await ReplyWithErrorAsync(found.Error!.Message);
        var (character, resource) = own;

        var current = resourceService.GetValue(character, resource);
        if (current < amount)
            return await ReplyWithErrorAsync($"{character.Name} only has {current} {resource.Name}.");

        await resourceService.SetValueAsync(character, resource, current - amount);

        return await ReplyAsync($"-{amount} {resource.Name}", $"{character.Name} spent {amount} {resource.Name} ({Format(character, resource)})");
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
        var found = await FindAsync(name);
        if (!found.IsDefined(out var own))
            return await ReplyWithErrorAsync(found.Error!.Message);
        var (character, resource) = own;

        var before = resourceService.GetValue(character, resource);
        var after = await resourceService.SetValueAsync(character, resource, value);

        var description = $"{character.Name}'s {resource.Name} is now {Format(character, resource)}";
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
            var found = await FindAsync(name, character);
            if (!found.IsDefined(out var own))
                return await ReplyWithErrorAsync(found.Error!.Message);
            toReset = [own.Resource];
        }

        await resourceService.ResetAsync(character, toReset);

        var notes = toReset.Where(r => r.ResetNote is not null).Select(r => r.ResetNote!).ToList();
        var summary = string.Join('\n', toReset.Select(r => $"**{r.Name}:** {Format(character, r)}"));

        return await ReplyAsync("Resources Reset", notes.Count > 0 ? $"{summary}\n\n{string.Join('\n', notes)}" : summary);
    }

    private string Format(Character character, ClassResource resource) => resourceService.Format(character, resource);

    private record OwnResource(Character Character, ClassResource Resource);

    /// <returns>The character and their resource, or why there's none to use.</returns>
    private async Task<Result<OwnResource>> FindAsync(string name, Character? character = null)
    {
        character ??= await GetOwnCharacterAsync();
        if (character is null)
            return new InvalidOperationError(NoCharacterMessage);

        var sheet = ClassSheetLibrary.For(character.Class.Type);
        if (ClassSheetLibrary.CommonResources.FirstOrDefault(r => r.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)) is { } common)
            return new OwnResource(character, common);

        var resource = sheet?.FindResource(name);
        if (resource is null)
            return new InvalidOperationError($"The {character.Class.Type} doesn't have a resource called {name}.");

        if (!sheet!.ResourcesFor(ClassSheetLibrary.KnownMoves(character)).Contains(resource))
            return new InvalidOperationError($"You need {resource.GrantedBy} before you have {resource.Name}.");

        return new OwnResource(character, resource);
    }

    private async Task<Character?> GetOwnCharacterAsync() =>
        await characterService.GetCharacterFromUserAsync(GetUserId());
}
