using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Pagination.Extensions;
using Remora.Results;

namespace DungeonWorldBot.Features.Spells;

[Group("spell")]
public class SpellCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    IAbilityService abilityService,
    IResourceService resourceService)
    : DungeonWorldCommandBase(context, feedbackService)
{
    // Spell text is long, so keep pages small enough to stay under Discord's embed size limit
    private const int SpellsPerPage = 6;

    [Command("list")]
    [Description("List the spells on a spell list, optionally just one level")]
    public async Task<IResult> ListAsync(
        [Description("The spell list")]
        SpellList list,
        [Description("Only show this level. Use 0 for rotes and cantrips")]
        [MinValue(0)]
        [MaxValue(9)]
        int? level = null)
    {
        var userId = GetUserId();

        var spells = SpellLists.For(list).Where(s => level is null || s.Level == level).ToList();
        if (spells.Count == 0)
            return await ReplyWithErrorAsync($"There are no level {level} {list.ToString().ToLower()} spells. Spells come in levels 1, 3, 5, 7 and 9.");

        var header = await CasterSummaryAsync(userId, list) ?? $"Learn one with /spell learn";
        var title = level is null ? $"{list} Spells" : $"{list} Spells: {spells[0].LevelLabel}";

        var pages = spells
            .Chunk(SpellsPerPage)
            .Select(page => new Embed(
                Title: title,
                Description: header,
                Colour: _feedbackService.Theme.Primary,
                Fields: page.Select(s => (IEmbedField)new EmbedField($"{s.Name} · {s.Heading}", Truncate(s.Description, 1024))).ToList()))
            .ToList();

        return await _feedbackService.SendContextualPaginatedMessageAsync(userId, pages);
    }

    [Command("info")]
    [Description("Read a spell")]
    public async Task<IResult> InfoAsync(
        [Description("The spell")]
        [AutocompleteProvider(SpellAutocomplete.ID)]
        string spell)
    {
        var matches = SpellLists.Find(spell);
        if (matches.Count == 0)
            return await ReplyWithErrorAsync($"There's no spell called {spell}.");

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Fields: matches.Select(s => (IEmbedField)new EmbedField($"{s.Name} · {s.Heading}", Truncate(s.Description, 1024))).ToList(),
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("learn")]
    [Description("Add a spell your deity grants you (Commune) or that you prepare")]
    public async Task<IResult> LearnAsync(
        [Description("The spell")]
        [AutocompleteProvider(SpellAutocomplete.ID)]
        string spell,
        [Option("ignore-limits")]
        [Description("Skip the level checks, e.g. for a spell your GM granted another way")]
        bool ignoreLimits = false)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        var access = SpellCasting.Access(character);
        var matches = SpellLists.Find(spell);
        if (matches.Count == 0)
            return await ReplyWithErrorAsync($"There's no spell called {spell}.");

        // For a name on more than one list (Light), use the list the character can cast from
        var chosen = matches.Count == 1 ? matches[0] : matches.FirstOrDefault(m => access.Any(a => a.List == m.List));
        if (chosen is null)
            return await ReplyWithErrorAsync($"More than one list has {matches[0].Name}. Say which, e.g. {string.Join(" or ", matches.Select(m => m.Qualified))}");

        var listAccess = access.FirstOrDefault(a => a.List == chosen.List);
        var known = SpellCasting.KnownSpells(character);

        if (!ignoreLimits)
        {
            if (listAccess is null)
            {
                return await ReplyWithErrorAsync(
                    $"{character.Name} can't cast {chosen.List.ToString().ToLower()} spells. A move like God Amidst the Wastes or Divine Favor grants them. " +
                    "If your GM has given you the spell another way, use ignore-limits.");
            }

            var casterLevel = resourceService.GetValue(character, listAccess.LevelResource);
            if (SpellCasting.WhyCannotLearn(chosen, casterLevel, known) is { } problem)
                return await ReplyWithErrorAsync(problem);
        }
        else if (known.Contains(chosen))
        {
            return await ReplyWithErrorAsync($"You already have {chosen.Name}.");
        }

        await abilityService.AddAbilityAsync(character, chosen.Name, $"{chosen.Heading}. {chosen.Description}\n{SpellCasting.ListNote(chosen.List)}", maxUses: null);

        var description = $"{character.Name} learned **{chosen.Name}** ({chosen.Heading})\n\n{chosen.Description}";
        if (listAccess is not null)
            description += $"\n\n{SpellLevelsLine(character, listAccess)}";

        return await ReplyAsync("Spell Learned!", Truncate(description, 4096));
    }

    [Command("known")]
    [Description("Show a character's spells and how many spell levels they're using")]
    public async Task<IResult> KnownAsync(
        [Description("The user to see the spells of. Don't specify a user to see your own.")]
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

        var known = SpellCasting.KnownSpells(character);
        var access = SpellCasting.Access(character);
        if (known.Count == 0 && access.Count == 0)
            return await ReplyAsync($"{character.Name}'s Spells", "No spells. A move like God Amidst the Wastes or Divine Favor lets a character cast them.");

        var fields = Enum.GetValues<SpellList>()
            .Where(list => known.Any(s => s.List == list) || access.Any(a => a.List == list))
            .Select(list =>
            {
                var spells = known.Where(s => s.List == list).OrderBy(s => s.Level).ThenBy(s => s.Name).ToList();
                var listAccess = access.FirstOrDefault(a => a.List == list);
                var heading = listAccess is not null ? $"{list} Spells · {SpellLevelsLine(character, listAccess)}" : $"{list} Spells";
                var value = spells.Count > 0 ? string.Join('\n', spells.Select(s => $"**{s.Name}** ({s.LevelLabel.ToLower()}{(s.IsOngoing ? ", ongoing" : "")})")) : "None yet";
                return (IEmbedField)new EmbedField(Truncate(heading, 256), Truncate(value, 1024));
            })
            .ToList();

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: $"{character.Name}'s Spells",
                Description: "Read one with /spell info, or cast it with /ability use",
                Fields: fields,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("clear")]
    [Description("Remove your granted or prepared spells (not rotes or cantrips), e.g. when you Commune")]
    public async Task<IResult> ClearAsync(
        [Description("The spell list to clear")]
        SpellList list)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        var toRemove = SpellCasting.KnownSpells(character).Where(s => s.List == list && !s.IsFree).ToList();
        foreach (var spell in toRemove)
        {
            if (abilityService.FindAbility(character, spell.Name) is { } ability)
                await abilityService.RemoveAbilityAsync(character, ability);
        }

        return await ReplyAsync("Spells Cleared", toRemove.Count > 0
            ? $"Removed {string.Join(", ", toRemove.Select(s => s.Name))}. Pick new ones with /spell learn"
            : $"{character.Name} had no {list.ToString().ToLower()} spells to clear.");
    }

    private string SpellLevelsLine(Character character, CasterAccess access)
    {
        var casterLevel = resourceService.GetValue(character, access.LevelResource);
        var used = SpellCasting.LevelsUsed(SpellCasting.KnownSpells(character), access.List);
        return $"{access.List} level {casterLevel}, {used}/{casterLevel + 1} spell levels used";
    }

    /// <returns>A line about the player's own casting from this list, or null if they can't cast from it.</returns>
    private async Task<string?> CasterSummaryAsync(Remora.Rest.Core.Snowflake userId, SpellList list)
    {
        var character = await characterService.GetCharacterFromUserAsync(userId);
        if (character is null || SpellCasting.Access(character).FirstOrDefault(a => a.List == list) is not { } access)
            return null;

        return $"{SpellLevelsLine(character, access)}. You can learn spells up to level {resourceService.GetValue(character, access.LevelResource)} with /spell learn";
    }

    private async Task<Character?> GetOwnCharacterAsync() =>
        await characterService.GetCharacterFromUserAsync(GetUserId());
}
