using System.Drawing;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Rolls;

public enum DramaKind
{
    LastBreathLived,
    LastBreathBargain,
    LastBreathDied,
    DoubleSixes,
    SnakeEyes,
    Comeback,
    DireMiss,
}

/// <summary>
/// A roll worth telling the table slowly: it's posted as the dice "rolling" first, then edited to land a moment later
/// with a headline over it (see <see cref="RollReveal"/>).
/// </summary>
/// <param name="Headline">The name of the field put above everything else in the roll, e.g. "⚡ DOUBLE SIXES ⚡".</param>
/// <param name="Line">A line of flavor under it.</param>
public record DramaticRoll(DramaKind Kind, string Headline, string Line, Color Colour)
{
    public bool IsLastBreath => Kind is DramaKind.LastBreathLived or DramaKind.LastBreathBargain or DramaKind.LastBreathDied;

    /// <summary>
    /// How long the dice "roll" for. Death takes a little longer to decide.
    /// </summary>
    public TimeSpan Pause => IsLastBreath ? TimeSpan.FromSeconds(2.5) : TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// What shows while the dice are rolling. Not titled with 🎲 and has no dice, so reading chat history never takes
    /// it for a roll if it's somehow left behind.
    /// </summary>
    /// <param name="who">The character, or the person if they have none.</param>
    /// <param name="what">What they're rolling, like the move, if anything.</param>
    public Embed Teaser(string who, string? what) => IsLastBreath
        ? new Embed(Description: $"🕯️ *{who} stands at the Black Gates of Death's Kingdom…*\n*Death is watching.*", Colour: Color.FromArgb(0x23, 0x27, 0x2A))
        : new Embed(Description: what is null ? $"🎲 *{who} rolls…*" : $"🎲 *{who} rolls {what}…*", Colour: Color.FromArgb(0x4F, 0x54, 0x5C));

    /// <summary>
    /// The roll as it lands: the headline first, then everything it already said, in the drama's color. The title and
    /// dice are left alone, since roll stats and reading chat history go by them.
    /// </summary>
    public Embed Reveal(Embed roll)
    {
        var fields = roll.Fields.IsDefined(out var existing) ? existing : [];
        return roll with
        {
            Fields = new Optional<IReadOnlyList<IEmbedField>>(fields.Prepend(new EmbedField(Headline, $"*{Line}*")).ToList()),
            Colour = Colour,
        };
    }
}

/// <summary>
/// Which rolls are dramatic, and what the table's told when they are.
/// </summary>
public static class DramaticRolls
{
    /// <summary>
    /// How many misses in a row a strong hit has to break to be a comeback.
    /// </summary>
    public const int ComebackAfter = 3;

    private static readonly string[] DoubleSixLines =
    [
        "The dice have spoken, and they're shouting.",
        "Somewhere, a bard starts writing a song about this.",
        "Even the DM sits back for a second.",
        "Fate leans in to get a better look.",
        "That's the kind of roll they tell stories about.",
        "The gods of chance are smiling. Make it count.",
    ];

    private static readonly string[] SnakeEyesLines =
    [
        "Somewhere, a god of misfortune smiles.",
        "The dice look away, embarrassed.",
        "Two little eyes, staring back. Blinking. Judging.",
        "That's going in the chronicle, and not kindly.",
        "The DM's eyes light up.",
        "At least it can't get any lower.",
    ];

    private static readonly string[] LivedLines =
    [
        "Death turns away. It isn't your time.",
        "The Black Gates close without you.",
        "You gasp back into the world, alive.",
    ];

    private static readonly string[] BargainLines =
    [
        "\"Not yet. But you'll owe me.\"",
        "Death names a price, and waits.",
        "\"I can let you go back. For something.\"",
    ];

    private static readonly string[] DiedLines =
    [
        "Your time has come.",
        "The Black Gates open, and you walk through.",
        "Death takes your hand, gently.",
    ];

    private static readonly string[] DireMissLines =
    [
        "It goes badly. It was always going to go badly.",
        "Bleeding, exhausted, and now this.",
        "The world goes very quiet for a moment.",
        "Whatever's coming next is going to hurt.",
    ];

    /// <summary>
    /// The drama in a roll, if there's any: Last Breath always, then double sixes, snake eyes, a strong hit after a run
    /// of misses, and a miss when they're nearly out of HP.
    /// </summary>
    /// <param name="move">The move rolled, if any.</param>
    /// <param name="outcome">What it counts as, after anything (like Tactics) that changes it.</param>
    /// <param name="missesBefore">How many misses in a row they rolled just before this.</param>
    /// <param name="health">Their character's health, if they rolled as one.</param>
    /// <param name="pick">Picks a line from a list of that many, at random unless a test says otherwise.</param>
    public static DramaticRoll? For(MoveDice dice, string? move, MoveOutcome outcome, int missesBefore = 0, Health? health = null,
        Func<int, int>? pick = null)
    {
        pick ??= Random.Shared.Next;
        string Line(string[] lines) => lines[pick(lines.Length)];

        if (string.Equals(move, BasicMoves.LastBreath.Name, StringComparison.OrdinalIgnoreCase))
        {
            return outcome switch
            {
                MoveOutcome.StrongHit => new DramaticRoll(DramaKind.LastBreathLived, "✨ Death turns away", Line(LivedLines), Color.FromArgb(0xF5, 0xE6, 0xB8)),
                MoveOutcome.WeakHit => new DramaticRoll(DramaKind.LastBreathBargain, "⚖️ Death offers a bargain", Line(BargainLines), Color.FromArgb(0x7B, 0x4B, 0xB8)),
                _ => new DramaticRoll(DramaKind.LastBreathDied, "⚰️ Death takes them", Line(DiedLines), Color.FromArgb(0x23, 0x27, 0x2A)),
            };
        }

        if (dice.IsNatural12)
            return new DramaticRoll(DramaKind.DoubleSixes, "⚡ DOUBLE SIXES ⚡", Line(DoubleSixLines), Color.FromArgb(0xF5, 0xB7, 0x00));
        if (dice.IsNatural2)
            return new DramaticRoll(DramaKind.SnakeEyes, "💀 Snake eyes", Line(SnakeEyesLines), Color.FromArgb(0x4B, 0x1D, 0x3F));
        if (outcome == MoveOutcome.StrongHit && missesBefore >= ComebackAfter)
            return new DramaticRoll(DramaKind.Comeback, "🔥 The tide turns", $"{missesBefore} misses in a row. Not this time.", Color.FromArgb(0xFF, 0x6A, 0x00));
        if (outcome == MoveOutcome.Miss && health is { CurrentHP: > 0, MaxHP: > 0 } hp && hp.CurrentHP * 4 <= hp.MaxHP)
            return new DramaticRoll(DramaKind.DireMiss, $"🩸 At {hp.CurrentHP} HP", Line(DireMissLines), Color.FromArgb(0x8B, 0x00, 0x00));

        return null;
    }

    /// <summary>
    /// Whether a field is a dramatic headline that's only true for the outcome rolled, like Death taking them, so
    /// changing the outcome (see <see cref="RollOutcomeChange"/>) should take it off. Double sixes stay double sixes.
    /// </summary>
    public static bool DependsOnOutcome(string fieldName) =>
        fieldName is "✨ Death turns away" or "⚖️ Death offers a bargain" or "⚰️ Death takes them" or "🔥 The tide turns"
        || fieldName.StartsWith("🩸 At ", StringComparison.Ordinal);
}
