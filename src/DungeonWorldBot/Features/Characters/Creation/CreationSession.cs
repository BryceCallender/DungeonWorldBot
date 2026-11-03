using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Rolls;

namespace DungeonWorldBot.Features.Characters.Creation;

public enum CreationSection { Class, Name, Race, Alignment, Stats, Gear, ClassChoices }

/// <summary>
/// One screen of character creation: a section, and for sections that ask several questions (gear, class choices),
/// which question.
/// </summary>
public record CreationScreen(CreationSection Section, int Question = 0)
{
    /// <summary>
    /// Carried on each menu and button, so ones left on an old screen can be told apart from the current one.
    /// </summary>
    // Remora splits custom IDs on colons, so the state has none
    public string State => $"{Section}{Question}";
}

/// <summary>
/// A player's character creation in progress: the draft, and the screens left to go through before the review.
/// </summary>
public class CreationSession(CharacterDraft draft)
{
    private static readonly IReadOnlyList<CreationSection> FirstPass =
    [
        CreationSection.Class, CreationSection.Name, CreationSection.Race, CreationSection.Alignment,
        CreationSection.Stats, CreationSection.Gear, CreationSection.ClassChoices,
    ];

    private IReadOnlyList<CreationSection> _plan = FirstPass;
    private int _position;
    private int _question;

    public CharacterDraft Draft { get; } = draft;

    public DateTimeOffset LastTouched { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether the player has seen the review, so finishing a section goes back to it.
    /// </summary>
    public bool HasReviewed { get; private set; }

    /// <summary>
    /// The screen being shown, or null for the review.
    /// </summary>
    public CreationScreen? Current => _position < _plan.Count ? new CreationScreen(_plan[_position], _question) : null;

    public string State => Current?.State ?? "Review";

    /// <summary>
    /// Whether the player has picked a class yet. Until then the draft holds the first class as a placeholder.
    /// </summary>
    public bool HasClass { get; private set; }

    public void ChooseClass(ClassKit kit, IDiceRoller dice)
    {
        // Picking the same class again keeps its gear and choices
        if (HasClass && kit == Draft.Kit)
            return;

        Draft.SetClass(kit, dice);
        HasClass = true;
    }

    /// <summary>
    /// Whether the current screen already has an answer, so the player can move on without picking again.
    /// </summary>
    public bool IsAnswered() => Current switch
    {
        null => true,
        { Section: CreationSection.Class } => HasClass,
        { Section: CreationSection.Name } => Draft.Name.Length > 0,
        { Section: CreationSection.Race } => Draft.Race != Race.Unknown,
        { Section: CreationSection.Alignment } => Draft.Alignment != Alignment.Unknown,
        { Section: CreationSection.Stats } => Draft.HasAllStats,
        { Section: CreationSection.Gear } screen => Draft.GearChoices.ContainsKey(screen.Question),
        { Section: CreationSection.ClassChoices } screen => HasAllPicks(ClassChoices(Draft)[screen.Question]),
        _ => false,
    };

    // An earlier choice can allow more picks here, so an answer from before it may be short
    private bool HasAllPicks(ClassFeature feature) =>
        Draft.FeatureChoices.TryGetValue(feature.Name, out var picks) && picks.Count == Draft.AllowedPicks(feature);

    /// <summary>
    /// Moves on to the next question, skipping sections with nothing to ask for this class.
    /// </summary>
    public void Next()
    {
        if (Current is null)
            return;

        if (_question + 1 < QuestionCount(_plan[_position]))
        {
            _question++;
            return;
        }

        _question = 0;
        _position++;
        SkipEmptySections(forward: true);
        HasReviewed |= Current is null;
    }

    /// <summary>
    /// Moves back a question. Going back from the first screen of an edit returns to the review.
    /// </summary>
    public void Back()
    {
        if (_question > 0)
        {
            _question--;
            return;
        }

        if (_position == 0)
        {
            if (HasReviewed)
                _position = _plan.Count;
            return;
        }

        _position--;
        SkipEmptySections(forward: false);
        _question = _position < _plan.Count ? Math.Max(0, QuestionCount(_plan[_position]) - 1) : 0;
    }

    public bool CanGoBack => _question > 0 || _position > 0 || HasReviewed;

    /// <summary>
    /// Goes from the review to a section. Changing class changes the gear and class choices on offer, so they're
    /// always asked again with it.
    /// </summary>
    public void Edit(CreationSection section)
    {
        _plan = section == CreationSection.Class
            ? [CreationSection.Class, CreationSection.Gear, CreationSection.ClassChoices]
            : [section];
        _position = 0;
        _question = 0;
        SkipEmptySections(forward: true);
    }

    /// <summary>
    /// The sections that have something to ask for the draft's class.
    /// </summary>
    public IEnumerable<CreationSection> EditableSections() =>
        FirstPass.Where(s => QuestionCount(s) > 0);

    private int QuestionCount(CreationSection section) => section switch
    {
        CreationSection.Gear => Draft.Kit.Questions.Count,
        CreationSection.ClassChoices => ClassChoices(Draft).Count,
        _ => 1,
    };

    /// <summary>
    /// The class sheet's choices with something a new character can pick.
    /// </summary>
    public static IReadOnlyList<ClassFeature> ClassChoices(CharacterDraft draft) =>
        draft.Sheet?.Features.Where(f => CharacterDraft.PickableOptions(f).Count > 0).ToList() ?? [];

    private void SkipEmptySections(bool forward)
    {
        while (_position >= 0 && _position < _plan.Count && QuestionCount(_plan[_position]) == 0)
            _position += forward ? 1 : -1;

        _position = Math.Max(_position, 0);
    }
}
