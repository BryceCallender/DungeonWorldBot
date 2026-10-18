using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Gear;
using DungeonWorldBot.Features.Leveling;
using DungeonWorldBot.Features.Npcs;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Spells;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Rules;
using Microsoft.Data.Sqlite;
using Remora.Rest.Core;

namespace DungeonWorldBot.Dashboard.Party;

/// <summary>
/// Reads the party out of the bot's database and works out what the bot would show for each character.
/// </summary>
public class PartyReader
{
    private readonly ICharacterService _characterService;
    private readonly IResourceService _resourceService;
    private readonly ILevelingService _levelingService;
    private readonly INpcService _npcService;

    public PartyReader(ICharacterService characterService, IResourceService resourceService, ILevelingService levelingService, INpcService npcService)
    {
        _characterService = characterService;
        _resourceService = resourceService;
        _levelingService = levelingService;
        _npcService = npcService;
    }

    public async Task<IReadOnlyList<CharacterView>> GetPartyAsync() =>
        (await _characterService.GetCharactersAsync())
            .OrderBy(c => c.Name)
            .Select(Describe)
            .ToList();

    /// <summary>
    /// The NPCs travelling with the party, in name order. None when the database predates NPCs.
    /// </summary>
    public async Task<IReadOnlyList<Npc>> GetNpcsAsync()
    {
        try
        {
            return await _npcService.GetAllAsync();
        }
        catch (SqliteException e) when (e.Message.Contains("no such table"))
        {
            // The dashboard only reads, so it can't add the table; the bot's migration does
            return [];
        }
    }

    /// <summary>
    /// A character by name as it appears in addresses ("mr-hatter"), or by Discord ID for older links.
    /// </summary>
    public async Task<CharacterView?> GetCharacterAsync(string nameOrId) =>
        ulong.TryParse(nameOrId, out var id) ? await GetCharacterAsync(id) : Match(await GetPartyAsync(), nameOrId);

    /// <inheritdoc cref="GetCharacterAsync(string)"/>
    public static CharacterView? Match(IEnumerable<CharacterView> party, string? nameOrId) =>
        string.IsNullOrWhiteSpace(nameOrId)
            ? null
            : party.FirstOrDefault(c => c.ID == nameOrId || c.Slug == Lookup.Slug(nameOrId));

    public async Task<CharacterView?> GetCharacterAsync(ulong id) =>
        await _characterService.GetCharacterFromUserAsync(new Snowflake(id)) is { } character ? Describe(character) : null;

    private CharacterView Describe(Character character)
    {
        var sheet = ClassSheetLibrary.For(character.Class.Type);
        var known = ClassSheetLibrary.KnownMoves(character);
        var subclass = SubclassLibrary.Of(character);
        var spells = KnownSpells(character);
        var spellGrants = SpellCasting.Access(character).ToDictionary(a => a.GrantedBy, a => a.List, StringComparer.OrdinalIgnoreCase);
        var gear = GearRules.Owned(character);

        // Abilities shown in their own sections rather than as moves
        var shownElsewhere = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        shownElsewhere.UnionWith(spells.Select(s => s.Name));
        shownElsewhere.UnionWith(gear.Select(g => g.Gear.Name));
        if (subclass is not null)
        {
            shownElsewhere.Add(subclass.Name);
            shownElsewhere.UnionWith(subclass.StateAbilities);
            shownElsewhere.UnionWith(subclass.Moves.Select(m => m.Move.Name));
        }

        return new CharacterView(
            character,
            ClassName(character.Class.Type),
            character.DamageModifier ?? character.Class.Damage,
            Stats(character),
            Armor(character),
            _levelingService.GetStatPoints(character),
            Resources(character, sheet, known, gear),
            subclass is null ? null : DescribeSubclass(character, subclass, known),
            SpellBooks(character, spells),
            gear.Select(g => new GearView(g.Gear.Name, g.Gear.ModeKind, g.Mode?.Name, g.Mode?.Title, g.Mode?.Effect)).ToList(),
            (character.Abilities ?? [])
                .Where(a => !shownElsewhere.Contains(a.Name))
                .OrderBy(a => a.ID)
                .Select(a => new AbilityLine(a.Name, a.Description, a.Uses, a.MaxUses, spellGrants.TryGetValue(a.Name, out var list) ? list : null))
                .ToList());
    }

    private static IReadOnlyList<StatLine> Stats(Character character) =>
        Enum.GetValues<StatType>()
            .Select(type => character.Stats?.FirstOrDefault(s => s.StatType == type) is { } stat
                ? new StatLine(
                    type,
                    stat.Value,
                    stat.Modifier,
                    DebilityRules.Has(character, DebilityRules.For(type)) ? DebilityRules.For(type) : null,
                    DebilityRules.IgnoringMove(character, type)?.Name)
                : null)
            .OfType<StatLine>()
            .ToList();

    private static ArmorView Armor(Character character) => new(
        DamageRules.Armor(character, ArmorType.Physical),
        DamageRules.Armor(character, ArmorType.Magical),
        DamageRules.Armor(character, ArmorType.Divine),
        DamageRules.BaseArmor(character, ArmorType.Physical),
        DamageRules.BaseArmor(character, ArmorType.Magical),
        DamageRules.BaseArmor(character, ArmorType.Divine),
        (character.ArmorBonuses ?? []).Select(DamageRules.Describe).ToList());

    private IReadOnlyList<ResourceLine> Resources(Character character, ClassSheet? sheet, IReadOnlyCollection<string> known, IReadOnlyList<OwnedGear> gear)
    {
        var own = sheet?.ResourcesFor(known) ?? [];
        var casting = SpellCasting.Access(character).Select(a => a.LevelResource);
        var counters = gear.Select(g => g.Mode?.Counter?.Resource).OfType<ClassResource>();

        return own.Concat(casting).Concat(counters)
            .DistinctBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new ResourceLine(r.Name, r.Summary, _resourceService.GetValue(character, r), _resourceService.GetMax(character, r)))
            .ToList();
    }

    /// <summary>
    /// The bot's known spells, plus spells saved as plain abilities before spells were tracked by list. Those are
    /// matched by name on a list the character casts from, and only when their description says so ("Level 1 cleric
    /// spell. ..."), since some moves share a spell's name (the Champion of Luna's Revelation).
    /// </summary>
    private static IReadOnlyList<Spell> KnownSpells(Character character)
    {
        var tracked = SpellCasting.KnownSpells(character);
        var lists = SpellCasting.Access(character).Select(a => a.List).ToHashSet();

        var untracked = (character.Abilities ?? [])
            .Where(a => !tracked.Any(s => s.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(a => SpellLists.Find(a.Name).FirstOrDefault(s =>
                lists.Contains(s.List)
                && a.Description?.Contains($"{s.List} spell", StringComparison.OrdinalIgnoreCase) == true))
            .OfType<Spell>();

        return tracked.Concat(untracked).ToList();
    }

    private IReadOnlyList<SpellBook> SpellBooks(Character character, IReadOnlyList<Spell> known) =>
        SpellCasting.Access(character)
            .Select(a => new SpellBook(
                a.List,
                a.GrantedBy,
                _resourceService.GetValue(character, a.LevelResource),
                SpellCasting.LevelsUsed(known, a.List),
                known.Where(s => s.List == a.List).OrderBy(s => s.Level).ThenBy(s => s.Name).ToList()))
            .ToList();

    private static SubclassView DescribeSubclass(Character character, Subclass subclass, IReadOnlyCollection<string> known) =>
        new(
            subclass.Name,
            subclass.Title,
            subclass.Status?.Invoke(character),
            subclass.Moves
                .Select(m => new SubclassMoveLine(
                    m.Level,
                    m.Move.Name,
                    m.Move.Summary,
                    known.Contains(m.Move.Name, StringComparer.OrdinalIgnoreCase) || (!m.Hidden && m.Level <= character.Level),
                    m.Hidden))
                .ToList());

    public static string ClassName(ClassType type) => Spaced(type.ToString());

    /// <summary>
    /// e.g. "SkyDancer" → "Sky Dancer".
    /// </summary>
    public static string Spaced(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? $" {c}" : c.ToString()));
}
