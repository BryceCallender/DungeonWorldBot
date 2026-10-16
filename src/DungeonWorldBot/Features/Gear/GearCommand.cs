using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Gear;

/// <summary>
/// Homebrew gear with its own rules, like the Gem-Aspected Bow.
/// </summary>
[Group("gear")]
public class GearCommand : DungeonWorldCommandBase
{
    private readonly ICharacterService _characterService;
    private readonly IGearService _gearService;
    private readonly IDamageService _damageService;
    private readonly IGameMasterService _gameMaster;

    public GearCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        IGearService gearService,
        IDamageService damageService,
        IGameMasterService gameMaster)
        : base(context, feedbackService)
    {
        _characterService = characterService;
        _gearService = gearService;
        _damageService = damageService;
        _gameMaster = gameMaster;
    }

    [Command("list")]
    [Description("List every piece of custom gear")]
    public async Task<IResult> ListAsync()
    {
        var fields = GearLibrary.All
            .Select(g => (IEmbedField)new EmbedField(g.Name, Truncate(
                $"{g.Summary}\n{g.ModeKind}s: {string.Join(", ", g.Modes.Select(m => m.Name))}", 1024)))
            .ToList();

        return await ReplyAsync("Custom Gear", "Read one with /gear info, and get it with /gear give", fields);
    }

    [Command("info")]
    [Description("Read a piece of custom gear, or see your own")]
    public async Task<IResult> InfoAsync(
        [Description("The gear to read. Leave blank to see your own.")]
        [AutocompleteProvider(GearAutocomplete.ID)]
        string? gear = null,
        [Description("See another player's gear")]
        IUser? user = null)
    {
        if (gear is not null)
        {
            if (GearLibrary.Find(gear) is not { } found)
                return await ReplyWithErrorAsync($"There's no custom gear called {gear}. See /gear list");

            return await ReplyAsync(found.Name, Describe(found), ModeFields(found, active: null));
        }

        var character = await GetCharacterAsync(user);
        if (character is null)
            return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");

        var owned = GearRules.Owned(character);
        if (owned.Count == 0)
            return await ReplyWithErrorAsync($"{character.Name} has no custom gear. See /gear list");

        var fields = owned
            .Select(o => (IEmbedField)new EmbedField(o.Gear.Name, Truncate(Status(character, o), 1024)))
            .ToList();

        return await ReplyAsync($"{character.Name}'s Gear", "Change modes with /gear socket", fields);
    }

    [Command("give")]
    [Description("Give a character a piece of custom gear (the DM can give it to someone else)")]
    public async Task<IResult> GiveAsync(
        [Description("The gear to give")]
        [AutocompleteProvider(GearAutocomplete.ID)]
        string gear,
        [Description("DM only: the player who gets it. Leave blank for yourself.")]
        IUser? user = null)
    {
        if (await CheckOthersAsync(user) is { } denied)
            return denied;

        var character = await GetCharacterAsync(user);
        if (character is null)
            return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");

        if (GearLibrary.Find(gear) is not { } found)
            return await ReplyWithErrorAsync($"There's no custom gear called {gear}. See /gear list");

        if (GearRules.Owned(character).Any(o => o.Gear == found))
            return await ReplyWithErrorAsync($"{character.Name} already has the {found.Name}.");

        await _gearService.GiveAsync(character, found);
        return await ReplyAsync($"{character.Name} gains the {found.Name}!",
            $"{found.Summary}\n\nChoose a {found.ModeKind.ToLower()} with /gear socket");
    }

    [Command("take")]
    [Description("Take a piece of custom gear away from a character (the DM can take it from someone else)")]
    public async Task<IResult> TakeAsync(
        [Description("The gear to take away")]
        [AutocompleteProvider(GearAutocomplete.ID)]
        string gear,
        [Description("DM only: the player who loses it. Leave blank for yourself.")]
        IUser? user = null)
    {
        if (await CheckOthersAsync(user) is { } denied)
            return denied;

        var character = await GetCharacterAsync(user);
        if (character is null)
            return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");

        if (GearLibrary.Find(gear) is not { } found || GearRules.Owned(character).All(o => o.Gear != found))
            return await ReplyWithErrorAsync($"{character.Name} doesn't have {gear}.");

        await _gearService.RemoveAsync(character, found);
        return await ReplyAsync("Gear Removed", $"{character.Name} no longer has the {found.Name}.");
    }

    [Command("socket")]
    [Description("Change your gear's mode, like socketing a different gem in the Gem-Aspected Bow")]
    public async Task<IResult> SocketAsync(
        [Description("The mode to switch to, e.g. Ruby")]
        [AutocompleteProvider(GearModeAutocomplete.ID)]
        string mode)
    {
        var character = await GetCharacterAsync(null);
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        var owned = GearRules.Owned(character).FirstOrDefault(o => o.Gear.FindMode(mode) is not null);
        if (owned is null)
            return await ReplyWithErrorAsync($"None of {character.Name}'s gear has a {mode} mode. See /gear info");

        var newMode = owned.Gear.FindMode(mode)!;
        if (owned.Mode == newMode)
            return await ReplyWithErrorAsync($"The {owned.Gear.Name} already has {newMode.Name} socketed.");

        await _gearService.SetModeAsync(character, owned.Gear, newMode);

        var description = $"*{newMode.Visual}*\n\n**{newMode.Title}:** {newMode.Effect}";
        if (owned.Mode?.Counter is { } lost && owned.Mode.Counter != newMode.Counter)
            description += $"\n\n{lost.Name} from the {owned.Mode.Name} fade{(lost.Name.EndsWith('s') ? "" : "s")}.";
        if (newMode.Counter is { } counter)
            description += $"\n\nTrack {counter.Name} with /gear counter";
        if (newMode.Action is { } action)
            description += $"\n\nUse {action.Name} with /gear action";
        if (owned.Gear.SwapNote is not null)
            description += $"\n\n*{owned.Gear.SwapNote}*";

        return await ReplyAsync($"{character.Name} sockets the {newMode.Name}", description);
    }

    [Command("counter")]
    [Description("Gain or spend something your gear tracks, like a Ruby's Dodges or Emerald Marks")]
    public async Task<IResult> CounterAsync(
        [Description("What to change, e.g. Dodge")]
        [AutocompleteProvider(GearCounterAutocomplete.ID)]
        string counter,
        [Description("How much to change it by: -1 to spend one, 1 to gain one")]
        int change)
    {
        var character = await GetCharacterAsync(null);
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        var active = GearRules.Owned(character)
            .FirstOrDefault(o => o.Mode?.Counter?.Name.Equals(counter.Trim(), StringComparison.OrdinalIgnoreCase) == true);
        if (active?.Mode?.Counter is not { } found)
            return await ReplyWithErrorAsync($"{character.Name} has nothing socketed that tracks {counter}.");

        var before = _gearService.GetCounter(character, found);
        if (before + change < 0)
            return await ReplyWithErrorAsync($"{character.Name} only has {before} {found.Name}.");

        var after = await _gearService.SetCounterAsync(character, found, before + change);
        var max = found.Max is { } m ? $"/{m}" : string.Empty;
        var description = $"{found.Name}: {before} → **{after}{max}**";
        if (after != before + change)
            description += $"\n*You can only hold {found.Max}.*";
        if (change < 0 && found.Summary.Length > 0)
            description += $"\n\n{found.Summary}";

        return await ReplyAsync($"{character.Name}: {found.Name}", description);
    }

    [Command("end-combat")]
    [Description("Clear anything your gear only tracks until the end of combat, like Emerald Marks")]
    public async Task<IResult> EndCombatAsync()
    {
        var character = await GetCharacterAsync(null);
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        var cleared = new List<string>();
        foreach (var counter in GearRules.Owned(character).SelectMany(o => o.Gear.Modes).Select(m => m.Counter).OfType<GearCounter>()
                     .Where(c => c.ClearsAfterCombat).Distinct())
        {
            if (_gearService.GetCounter(character, counter) == 0)
                continue;

            await _gearService.SetCounterAsync(character, counter, 0);
            cleared.Add(counter.Name);
        }

        return await ReplyAsync("Combat Over", cleared.Count > 0 ? $"Cleared {string.Join(", ", cleared)}." : "Nothing to clear.");
    }

    [Command("action")]
    [Description("Use your gear's special attack, like the Topaz's Opportunist's Volley")]
    public async Task<IResult> ActionAsync()
    {
        var character = await GetCharacterAsync(null);
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        var owned = GearRules.Owned(character).FirstOrDefault(o => o.Mode?.Action is not null);
        if (owned?.Mode?.Action is not { } action)
            return await ReplyWithErrorAsync($"Nothing {character.Name} has socketed gives a special attack. Topaz gives the bow Opportunist's Volley.");

        if (await _damageService.RollAsync(character, action.Name) is not { } damage)
            return await ReplyWithErrorAsync("Couldn't roll your damage. Fix it with /character damage-dice");

        var dealt = action.HalfDamage ? GearRules.Half(damage.Total) : damage.Total;
        var damageText = $"{damage.Expression} → {damage.Total}" +
                         (action.HalfDamage ? $", halved (rounded up) → **{dealt}**" : $" → **{dealt}**") +
                         damage.Details;

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: $"🏹 {character.Name}: {action.Name}",
                Description: $"*{action.Trigger}...*\n*{owned.Mode.Visual}*\n\n{DiceHelper.BuildDiceResult(damage.Dice)}",
                Fields: new List<IEmbedField> { new EmbedField("Damage", damageText) },
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    private static string Describe(CustomGear gear) =>
        gear.Summary + (gear.SwapNote is not null ? $"\n*{gear.SwapNote}*" : string.Empty);

    private static IReadOnlyList<IEmbedField> ModeFields(CustomGear gear, GearMode? active) =>
        gear.Modes
            .Select(m => (IEmbedField)new EmbedField(
                Truncate($"{(m == active ? "💎 " : string.Empty)}{m.Name}: {m.Title}", 256),
                Truncate($"{m.Effect}\n*{m.Visual}*", 1024)))
            .ToList();

    private string Status(Character character, OwnedGear owned)
    {
        if (owned.Mode is not { } mode)
            return $"No {owned.Gear.ModeKind.ToLower()} socketed. Choose one with /gear socket";

        var status = $"**{mode.Name}: {mode.Title}**\n{mode.Effect}";
        if (mode.Counter is { } counter)
            status += $"\n**{counter.Name}:** {_gearService.GetCounter(character, counter)}{(counter.Max is { } max ? $"/{max}" : string.Empty)}";
        return status;
    }

    /// <returns>An error if the user is acting on someone else's character without being the DM.</returns>
    private async Task<IResult?> CheckOthersAsync(IUser? user)
    {
        if (user is null || !_context.TryGetUserID(out var self) || user.ID == self || await _gameMaster.IsGameMasterAsync(CancellationToken))
            return null;

        return await ReplyWithErrorAsync($"Only someone with the {_gameMaster.RoleName} role can change someone else's gear.");
    }

    private async Task<Character?> GetCharacterAsync(IUser? user)
    {
        if (user is not null)
            return await _characterService.GetCharacterFromUserAsync(user.ID);

        var userId = GetUserId();

        return await _characterService.GetCharacterFromUserAsync(userId);
    }
}
