using System.Drawing;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.Guides;
using DungeonWorldBot.Infrastructure;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Interactivity;
using Remora.Rest.Core;
using Ids = DungeonWorldBot.Features.Characters.Creation.CharacterCreationInteractions;

namespace DungeonWorldBot.Features.Characters.Creation;

/// <summary>
/// A screen of character creation as a Discord message.
/// </summary>
public record CreationMessage(Embed Embed, IReadOnlyList<IMessageComponent> Components);

/// <summary>
/// Draws each screen of character creation: an embed saying what to pick, a menu or button to pick it with, and
/// buttons to go back, move on or cancel.
/// </summary>
public static class CreationView
{
    private const string Title = "Character Creation";

    // Discord's limits on menus and embeds
    private const int MaxOptions = 25;
    private const int MaxOptionText = 100;
    private const int MaxFieldValue = 1024;
    private const int MaxEmbedText = 5500;

    private static readonly Color NoClassColor = Color.Goldenrod;

    private static readonly IReadOnlyDictionary<StatType, string> StatNames = new Dictionary<StatType, string>
    {
        [StatType.Str] = "Strength",
        [StatType.Dex] = "Dexterity",
        [StatType.Con] = "Constitution",
        [StatType.Int] = "Intelligence",
        [StatType.Wis] = "Wisdom",
        [StatType.Cha] = "Charisma",
    };

    // What everyone rolls each stat for, whatever their class
    private static readonly IReadOnlyDictionary<StatType, string> BasicUses = new Dictionary<StatType, string>
    {
        [StatType.Str] = "Hack and Slash, and Defy Danger by powering through",
        [StatType.Dex] = "Volley, and Defy Danger by getting out of the way",
        [StatType.Con] = "Your max HP, and Defy Danger by enduring",
        [StatType.Int] = "Spout Lore, and Defy Danger with quick thinking",
        [StatType.Wis] = "Discern Realities, and Defy Danger with willpower",
        [StatType.Cha] = "Parley, and Defy Danger by charming",
    };

    private static readonly IReadOnlyDictionary<CreationSection, string> SectionNames = new Dictionary<CreationSection, string>
    {
        [CreationSection.Class] = "Class",
        [CreationSection.Name] = "Name",
        [CreationSection.Race] = "Race",
        [CreationSection.Alignment] = "Alignment",
        [CreationSection.Stats] = "Stats",
        [CreationSection.Gear] = "Starting Gear",
        [CreationSection.ClassChoices] = "Class Choices",
    };

    public static string SectionName(CreationSection section) => SectionNames[section];

    public static string StatName(StatType stat) => StatNames[stat];

    /// <param name="problem">Why the last answer wasn't taken, shown above the question.</param>
    public static CreationMessage Render(CreationSession session, Snowflake userId, string? problem = null)
    {
        var message = session.Current switch
        {
            null => Review(session, userId),
            { Section: CreationSection.Class } => ClassScreen(session),
            { Section: CreationSection.Name } => NameScreen(session),
            { Section: CreationSection.Race } => RaceScreen(session),
            { Section: CreationSection.Alignment } => AlignmentScreen(session),
            { Section: CreationSection.Stats } => StatsScreen(session),
            { Section: CreationSection.Gear } screen => GearScreen(session, screen.Question),
            { Section: CreationSection.ClassChoices } screen => ClassChoiceScreen(session, screen.Question),
            _ => throw new ArgumentOutOfRangeException(nameof(session)),
        };

        if (problem is null)
            return message;

        var fields = message.Embed.Fields.OrDefault([]).Prepend(new EmbedField("⚠️ Not quite", problem)).ToList();
        return message with { Embed = message.Embed with { Fields = fields } };
    }

    /// <summary>
    /// The character as saved, with nothing left to pick.
    /// </summary>
    public static CreationMessage Created(Character character) => new(
        new Embed
        {
            Title = character.Name,
            Description = $"{character.Class.Type}: Level 1\n\nSaved! See your character sheet any time with `/character profile`, " +
                          $"and your class's moves with `/class sheet`.",
            Fields = CharacterSheet.NewCharacterFields(character),
            Colour = ColorOf(character.Class.Type),
        },
        []);

    public static CreationMessage Ended(string text) =>
        new(new Embed { Title = Title, Description = text, Colour = Color.Gray }, []);

    private static CreationMessage ClassScreen(CreationSession session)
    {
        var options = ClassKits.All
            .Select((kit, i) => Option(kit.DisplayName, i, ClassPitch(kit.Type), session.HasClass && kit == session.Draft.Kit))
            .ToList();

        var description = "Pick your class. It decides your moves, your gear and how much damage you can take." +
                          (session.HasClass ? $"\n\n**{session.Draft.Kit.DisplayName}**: {ClassPitch(session.Draft.Kit.Type)}" : string.Empty);

        return Screen(session, description, [], Select(Ids.Class, session, options, "Choose a class"));
    }

    private static CreationMessage NameScreen(CreationSession session)
    {
        var named = session.Draft.Name.Length > 0;
        var description = named
            ? $"Your character is called **{session.Draft.Name}**."
            : "What's your character called? Every character in the campaign needs a different name.";

        var button = new ButtonComponent(
            ButtonComponentStyle.Primary,
            named ? "Change name" : "Enter a name",
            CustomID: CustomIDHelpers.CreateButtonIDWithState(Ids.Name, session.State));

        return Screen(session, description, [], new ActionRowComponent([button]));
    }

    private static CreationMessage RaceScreen(CreationSession session)
    {
        var options = CharacterDraft.RaceOptions
            .Select((o, i) => Option(o.Label, i, null, o.Value == session.Draft.Race))
            .ToList();

        return Screen(session, "What is your character's race?", [], Select(Ids.Race, session, options, "Choose a race"));
    }

    private static CreationMessage AlignmentScreen(CreationSession session)
    {
        var options = CharacterDraft.AlignmentOptions
            .Select((o, i) => Option(o.Label, i, null, o.Value == session.Draft.Alignment))
            .ToList();

        return Screen(session, "What drives your character? Pick an alignment.", [], Select(Ids.Alignment, session, options, "Choose an alignment"));
    }

    private static CreationMessage StatsScreen(CreationSession session)
    {
        var draft = session.Draft;
        var uses = ClassUses(draft);

        List<IEmbedField> fields = CharacterDraft.StatOrder
            .Select(stat => (IEmbedField)new EmbedField(StatName(stat), StatValue(draft, stat), IsInline: true))
            .ToList();

        if (draft.Stats.TryGetValue(StatType.Con, out var con))
        {
            var classHp = new Class { Type = draft.Kit.Type }.HealthModifier;
            fields.Add(new EmbedField("Health", $"{con} Constitution + {classHp} from {draft.Kit.DisplayName} = **{con + classHp} HP**"));
        }

        if (uses.Count > 0)
        {
            fields.Add(new EmbedField($"What a {draft.Kit.DisplayName} rolls", Truncate(
                string.Join('\n', uses.Select(u => $"**{u.Key.ToString().ToUpper()}**: {u.Value}")), MaxFieldValue)));
        }

        var description = "Give each stat one score from the array, best first. A stat's modifier is what you add to its rolls." +
                          (draft.HasAllStats ? "\n\nAll six are set. Press **Next** to keep them, or **Reassign** to start over." : string.Empty);

        IMessageComponent[] extra = [];
        if (draft.NextStatValue() is { } next)
        {
            var options = CharacterDraft.StatOrder
                .Where(stat => !draft.Stats.ContainsKey(stat))
                .Select(stat => Option(StatName(stat), (int)stat, uses.GetValueOrDefault(stat) ?? BasicUses[stat], false))
                .ToList();

            var modifier = Numbers.Signed(new Stat { Value = next }.Modifier);
            extra = [Select(Ids.Stat, session, options, $"Which stat gets your {next} ({modifier})?")];
        }

        return Screen(session, description, fields, extra, ResetStatsButton(session));
    }

    private static CreationMessage GearScreen(CreationSession session, int index)
    {
        var draft = session.Draft;
        var question = draft.Kit.Questions[index];
        var chosen = draft.GearChoices.GetValueOrDefault(index) ?? [];

        var options = question.Choices
            .Select((c, i) => Option(c.Label, i, ArmorAndDamage(c), chosen.Contains(c)))
            .ToList();

        List<IEmbedField> fields = draft.Items.Count > 0
            ? [new EmbedField("Packed so far", Truncate(string.Join('\n', draft.Items.Select(ItemLine)), MaxFieldValue))]
            : [];

        var placeholder = question.Picks > 1 ? $"Choose {question.Picks}" : "Choose one";
        return Screen(session, $"**{question.Prompt}**", fields, Select(Ids.Gear, session, options, placeholder, question.Picks));
    }

    private static CreationMessage ClassChoiceScreen(CreationSession session, int index)
    {
        var draft = session.Draft;
        var feature = CreationSession.ClassChoices(draft)[index];
        var pickable = CharacterDraft.PickableOptions(feature).Take(MaxOptions).ToList();
        var picks = draft.AllowedPicks(feature);
        var chosen = draft.FeatureChoices.GetValueOrDefault(feature.Name) ?? [];

        // Spread the room an embed has over the options, so long descriptions don't push it over Discord's limit
        var room = Math.Clamp(MaxEmbedText / Math.Max(1, pickable.Count), 80, MaxFieldValue);
        var fields = pickable
            .Select(o => (IEmbedField)new EmbedField(o.Name, Truncate(o.Summary, room)))
            .ToList();

        var options = pickable
            .Select((o, i) => Option(o.Name, i, o.Summary, chosen.Contains(o)))
            .ToList();

        var placeholder = picks > 1 ? $"Choose {picks}" : "Choose one";
        return Screen(session, $"**{feature.Name}**\n{feature.Prompt}", fields, Select(Ids.Choice, session, options, placeholder, picks));
    }

    private static CreationMessage Review(CreationSession session, Snowflake userId)
    {
        var preview = session.Draft.Build(userId);

        List<IEmbedField> fields =
        [
            new EmbedField("Name", preview.Name, IsInline: true),
            new EmbedField("Class", $"{session.Draft.Kit.DisplayName}, Level 1", IsInline: true),
        ];
        fields.AddRange(CharacterSheet.NewCharacterFields(preview));

        var edits = session.EditableSections()
            .Select(s => new SelectOption(SectionName(s), s.ToString()))
            .ToList();

        IMessageComponent[] components =
        [
            new ActionRowComponent([new StringSelectComponent(
                CustomIDHelpers.CreateSelectMenuIDWithState(Ids.Edit, session.State), edits, "Change something…")]),
            new ActionRowComponent(
            [
                new ButtonComponent(ButtonComponentStyle.Success, "Create character", CustomID: CustomIDHelpers.CreateButtonIDWithState(Ids.Create, session.State)),
                new ButtonComponent(ButtonComponentStyle.Danger, "Cancel", CustomID: CustomIDHelpers.CreateButtonIDWithState(Ids.Cancel, session.State)),
            ]),
        ];

        var embed = new Embed
        {
            Title = $"{Title}: Review",
            Description = "Here's your character. Change anything you like, then create them when you're happy.",
            Fields = fields,
            Colour = ColorOf(session.Draft.Kit.Type),
        };

        return new CreationMessage(embed, components);
    }

    /// <summary>
    /// A screen with its question, what's been picked, its menu and the buttons to get around.
    /// </summary>
    private static CreationMessage Screen(
        CreationSession session,
        string description,
        IReadOnlyList<IEmbedField> fields,
        IMessageComponent picker,
        params IReadOnlyList<ButtonComponent> extraButtons) =>
        Screen(session, description, fields, [picker], extraButtons);

    private static CreationMessage Screen(
        CreationSession session,
        string description,
        IReadOnlyList<IEmbedField> fields,
        IReadOnlyList<IMessageComponent> pickers,
        params IReadOnlyList<ButtonComponent> extraButtons)
    {
        var screen = session.Current!;
        var embed = new Embed
        {
            Title = $"{Title}: {SectionName(screen.Section)}",
            Description = description,
            Fields = fields.ToList(),
            Colour = session.HasClass ? ColorOf(session.Draft.Kit.Type) : NoClassColor,
            Footer = new EmbedFooter("Your draft is kept for a few hours. Run /character create again to pick up where you left off."),
        };

        return new CreationMessage(embed, [.. pickers, NavigationRow(session, extraButtons)]);
    }

    private static ActionRowComponent NavigationRow(CreationSession session, IReadOnlyList<ButtonComponent> extraButtons)
    {
        List<ButtonComponent> buttons =
        [
            new(ButtonComponentStyle.Secondary, "Back", CustomID: CustomIDHelpers.CreateButtonIDWithState(Ids.Back, session.State), IsDisabled: !session.CanGoBack),
        ];

        // Menus move on as soon as something is picked, so Next is only for keeping an earlier answer
        if (session.IsAnswered())
            buttons.Add(new(ButtonComponentStyle.Primary, "Next", CustomID: CustomIDHelpers.CreateButtonIDWithState(Ids.Next, session.State)));

        buttons.AddRange(extraButtons);
        buttons.Add(new(ButtonComponentStyle.Danger, "Cancel", CustomID: CustomIDHelpers.CreateButtonIDWithState(Ids.Cancel, session.State)));

        return new ActionRowComponent(buttons);
    }

    private static IReadOnlyList<ButtonComponent> ResetStatsButton(CreationSession session) =>
        session.Draft.Stats.Count > 0
            ? [new(ButtonComponentStyle.Secondary, session.Draft.HasAllStats ? "Reassign" : "Start over", CustomID: CustomIDHelpers.CreateButtonIDWithState(Ids.ResetStats, session.State))]
            : [];

    private static ActionRowComponent Select(string name, CreationSession session, IReadOnlyList<SelectOption> options, string placeholder, int picks = 1) =>
        new([new StringSelectComponent(
            CustomIDHelpers.CreateSelectMenuIDWithState(name, session.State),
            options.Take(MaxOptions).ToList(),
            placeholder,
            MinValues: picks,
            MaxValues: picks)]);

    private static SelectOption Option(string label, int value, string? description, bool isDefault) => new(
        Truncate(label, MaxOptionText),
        value.ToString(),
        description is null ? default(Optional<string>) : Truncate(description, MaxOptionText),
        IsDefault: isDefault);

    /// <summary>
    /// For each stat, the moves the class's sheet rolls it for, e.g. "Lay on Hands, Bolster".
    /// </summary>
    private static Dictionary<StatType, string> ClassUses(CharacterDraft draft) =>
        draft.Sheet is null
            ? []
            : ClassGuides.RolledMoves(draft.Sheet)
                .Where(m => m.Roll?.Stat is not null && m.RollsWith is null)
                .GroupBy(m => m.Roll!.Stat!.Value)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(m => m.Name).Distinct()));

    private static string StatValue(CharacterDraft draft, StatType stat) =>
        draft.Stats.TryGetValue(stat, out var value)
            ? $"{value} ({Numbers.Signed(new Stat { Value = value }.Modifier)})"
            : "—";

    private static string? ArmorAndDamage(KitChoice choice)
    {
        List<string> parts = [];
        if (choice.Armor != 0)
            parts.Add($"{Numbers.Signed(choice.Armor)} armor");
        if (choice.Damage != 0)
            parts.Add($"{Numbers.Signed(choice.Damage)} damage");

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    public static string ItemLine(Item item) => item.Amount == 1 ? item.Name : $"{item.Amount} × {item.Name}";

    private static string ClassPitch(ClassType type) =>
        ClassGuides.PitchFor(type) ?? ClassSheetLibrary.For(type)?.Source ?? type.ToString();

    private static Color ColorOf(ClassType type) => ClassSheetLibrary.For(type)?.Color.Discord ?? NoClassColor;

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..(length - 1)] + "…";
}
