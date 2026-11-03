using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters.Creation;
using DungeonWorldBot.Features.Rolls;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Characters.Creation;

public class CreationSessionTests
{
    private static readonly Snowflake User = new(1);
    private readonly IDiceRoller _dice = new DiceRoller();

    private static ClassKit Kit(ClassType type) => ClassKits.All.Single(k => k.Type == type);

    private CreationSession NewSession()
    {
        var draft = new CharacterDraft();
        draft.ResetGear(_dice);
        return new CreationSession(draft);
    }

    /// <summary>
    /// Answers the current screen with its first options, the way a player picking from the menus would.
    /// </summary>
    private void AnswerCurrent(CreationSession session, ClassType type)
    {
        var draft = session.Draft;
        switch (session.Current)
        {
            case { Section: CreationSection.Class }:
                session.ChooseClass(Kit(type), _dice);
                break;
            case { Section: CreationSection.Name }:
                draft.Name = "Aria";
                break;
            case { Section: CreationSection.Race }:
                draft.Race = Race.Human;
                break;
            case { Section: CreationSection.Alignment }:
                draft.Alignment = Alignment.TrueNeutral;
                break;
            case { Section: CreationSection.Stats }:
                foreach (var stat in CharacterDraft.StatOrder)
                    draft.AssignStat(stat, draft.NextStatValue()!.Value);
                break;
            case { Section: CreationSection.Gear } screen:
                var question = draft.Kit.Questions[screen.Question];
                draft.ChooseGear(screen.Question, question.Choices.Take(question.Picks), _dice);
                break;
            case { Section: CreationSection.ClassChoices } screen:
                var feature = CreationSession.ClassChoices(draft)[screen.Question];
                draft.FeatureChoices[feature.Name] = CharacterDraft.PickableOptions(feature).Take(draft.AllowedPicks(feature)).ToList();
                break;
        }
    }

    private List<CreationScreen> WalkToReview(CreationSession session, ClassType type, Action<CreationSession>? onEachScreen = null)
    {
        List<CreationScreen> seen = [];
        while (session.Current is { } screen)
        {
            Assert.True(seen.Count < 100, "Creation never reached the review");
            seen.Add(screen);
            onEachScreen?.Invoke(session);
            AnswerCurrent(session, type);
            Assert.True(session.IsAnswered(), $"{screen} isn't answered after picking");
            session.Next();
        }

        return seen;
    }

    [Fact]
    public void FirstPass_AsksClassFirst_ThenEverythingElse_ThenReviews()
    {
        var session = NewSession();

        var seen = WalkToReview(session, ClassType.Paladin);

        Assert.Equal(
            [CreationSection.Class, CreationSection.Name, CreationSection.Race, CreationSection.Alignment, CreationSection.Stats, CreationSection.Gear],
            seen.Select(s => s.Section).Distinct().Take(6));
        Assert.Equal(Kit(ClassType.Paladin).Questions.Count, seen.Count(s => s.Section == CreationSection.Gear));
        Assert.True(session.HasReviewed);
    }

    [Fact]
    public void Next_DoesNotMoveOnUntilClassIsPicked()
    {
        var session = NewSession();

        Assert.False(session.IsAnswered());
        Assert.False(session.CanGoBack);
    }

    [Fact]
    public void Back_ReturnsToTheLastQuestionOfThePreviousSection()
    {
        var session = NewSession();
        WalkToReview(session, ClassType.Paladin);
        session.Edit(CreationSection.Class);
        session.Next(); // to the first gear question
        session.Next(); // to the second

        session.Back();
        session.Back();

        Assert.Equal(new CreationScreen(CreationSection.Class), session.Current);
    }

    [Fact]
    public void Back_FromTheFirstScreenOfAnEdit_ReturnsToReview()
    {
        var session = NewSession();
        WalkToReview(session, ClassType.Ranger);

        session.Edit(CreationSection.Race);
        session.Back();

        Assert.Null(session.Current);
    }

    [Fact]
    public void EditingASection_ReturnsToReviewOnceAnswered()
    {
        var session = NewSession();
        WalkToReview(session, ClassType.Ranger);

        session.Edit(CreationSection.Alignment);
        session.Draft.Alignment = Alignment.ChaoticGood;
        session.Next();

        Assert.Null(session.Current);
        Assert.Equal(Alignment.ChaoticGood, session.Draft.Alignment);
    }

    [Fact]
    public void EditingClass_AsksGearAndClassChoicesAgain()
    {
        var session = NewSession();
        WalkToReview(session, ClassType.Ranger);

        session.Edit(CreationSection.Class);
        var seen = WalkToReview(session, ClassType.Battlemaster);

        Assert.Equal(CreationSection.Class, seen[0].Section);
        Assert.Contains(seen, s => s.Section == CreationSection.Gear);
        Assert.Contains(seen, s => s.Section == CreationSection.ClassChoices);
        Assert.DoesNotContain(seen, s => s.Section == CreationSection.Name);
        Assert.Equal(ClassType.Battlemaster, session.Draft.Kit.Type);
    }

    [Fact]
    public void PickingTheSameClassAgain_KeepsItsGear()
    {
        var session = NewSession();
        WalkToReview(session, ClassType.Paladin);
        var items = session.Draft.Items.Select(i => i.Name).ToList();

        session.ChooseClass(Kit(ClassType.Paladin), _dice);

        Assert.Equal(items, session.Draft.Items.Select(i => i.Name));
        Assert.NotEmpty(session.Draft.GearChoices);
    }

    [Fact]
    public void Stats_AreAssignedBestFirst()
    {
        var draft = new CharacterDraft();

        Assert.Equal(16, draft.NextStatValue());
        draft.AssignStat(StatType.Wis, 16);
        Assert.Equal(15, draft.NextStatValue());
        Assert.False(draft.HasAllStats);
    }

    [Fact]
    public void AssigningATakenValue_MovesItToTheNewStat()
    {
        var draft = new CharacterDraft();
        draft.AssignStat(StatType.Str, 16);

        draft.AssignStat(StatType.Dex, 16);

        Assert.False(draft.Stats.ContainsKey(StatType.Str));
        Assert.Equal(16, draft.Stats[StatType.Dex]);
    }

    [Fact]
    public void AnsweringAGearQuestionAgain_ReplacesTheEarlierAnswer()
    {
        var draft = new CharacterDraft();
        draft.SetClass(Kit(ClassType.Paladin), _dice);
        var weapons = draft.Kit.Questions[0];

        draft.ChooseGear(0, [weapons.Choices[1]], _dice); // long sword and shield
        draft.ChooseGear(0, [weapons.Choices[0]], _dice); // halberd

        Assert.Equal(2, draft.ArmorRating);
        Assert.DoesNotContain(draft.Items, i => i.Name == "Shield");
        Assert.Contains(draft.Items, i => i.Name.StartsWith("Halberd"));
    }

    [Theory]
    [MemberData(nameof(AllClasses))]
    public void EveryScreen_FitsDiscordsLimits(ClassType type)
    {
        var session = NewSession();

        WalkToReview(session, type, s => AssertFits(CreationView.Render(s, User, "A problem to show")));
        AssertFits(CreationView.Render(session, User));
        AssertFits(CreationView.Created(session.Draft.Build(User)));
    }

    public static TheoryData<ClassType> AllClasses() => new(Enum.GetValues<ClassType>());

    private static void AssertFits(CreationMessage message)
    {
        var embed = message.Embed;
        var fields = embed.Fields.OrDefault([]);
        var length = embed.Title.OrDefault("").Length + embed.Description.OrDefault("").Length
                     + fields.Sum(f => f.Name.Length + f.Value.Length)
                     + (embed.Footer.HasValue ? embed.Footer.Value.Text.Length : 0);

        Assert.True(length <= 6000, $"Embed is {length} characters");
        Assert.True(fields.Count <= 25, $"Embed has {fields.Count} fields");
        Assert.All(fields, f => Assert.True(f.Value.Length is > 0 and <= 1024, $"{f.Name} is {f.Value.Length} characters"));
        Assert.True(embed.Description.OrDefault("x").Length <= 4096);

        Assert.True(message.Components.Count <= 5);
        foreach (var row in message.Components.Cast<ActionRowComponent>())
        {
            Assert.InRange(row.Components.Count, 1, 5);
            foreach (var select in row.Components.OfType<StringSelectComponent>())
            {
                Assert.InRange(select.Options.Count, 1, 25);
                Assert.True(select.CustomID.Length <= 100);
                Assert.True(select.MaxValues.OrDefault(1) <= select.Options.Count, "Asks for more picks than there are options");
                Assert.All(select.Options, o =>
                {
                    Assert.InRange(o.Label.Length, 1, 100);
                    Assert.True(o.Description.OrDefault("").Length <= 100);
                });
            }
        }
    }
}
