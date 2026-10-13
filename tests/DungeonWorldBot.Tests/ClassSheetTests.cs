using DungeonWorldBot.CharacterCreation;
using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Rolling;
using DungeonWorldBot.Services.Implementation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests;

public class ClassSheetTests
{
    public static TheoryData<ClassType> SheetClasses() => new(ClassSheetLibrary.All.Select(s => s.Type));

    private static ClassSheet Sheet(ClassType type) => ClassSheetLibrary.For(type)!;

    [Fact]
    public void EachClassHasAtMostOneSheet()
    {
        Assert.Equal(ClassSheetLibrary.All.Count, ClassSheetLibrary.All.Select(s => s.Type).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(SheetClasses))]
    public void MoveNamesAreUnique(ClassType type)
    {
        var duplicates = Sheet(type).AllMoves.GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key);
        Assert.Empty(duplicates);
    }

    [Theory]
    [MemberData(nameof(SheetClasses))]
    public void ReferencedMovesAndResourcesExist(ClassType type)
    {
        var sheet = Sheet(type);

        Assert.All(sheet.AllMoves.SelectMany(m => m.RequiredMoves), r => Assert.NotNull(sheet.FindMove(r)));
        Assert.All(sheet.AllMoves.Where(m => m.Replaces is not null), m => Assert.NotNull(sheet.FindMove(m.Replaces!)));
        Assert.All(sheet.AllMoves.Where(m => m.RollsWith is not null), m => Assert.True(sheet.FindResource(m.RollsWith!) is not null || sheet.ClassStat?.Name == m.RollsWith, m.Name));
        Assert.All(sheet.Resources.Where(r => r.GrantedBy is not null), r => Assert.NotNull(sheet.FindMove(r.GrantedBy!)));
        Assert.All(sheet.Features.SelectMany(f => f.ExtraPicks), p => Assert.NotNull(sheet.FindMove(p.Move)));
    }

    [Theory]
    [MemberData(nameof(SheetClasses))]
    public void MovesFitInDiscord(ClassType type)
    {
        // Autocomplete values are capped at 100 characters and embed field values at 1024
        Assert.All(Sheet(type).AllMoves, m =>
        {
            Assert.InRange(m.Name.Length, 1, 100);
            Assert.InRange(m.Summary.Length, 1, 1024);
        });
    }

    [Theory]
    [MemberData(nameof(SheetClasses))]
    public void EverySheetClassHasAKit(ClassType type)
    {
        Assert.Contains(ClassKits.All, k => k.Type == type);
    }

    [Fact]
    public void RolledMoveSummary_DescribesTheRoll()
    {
        var channel = Sheet(ClassType.Channeler).FindMove("channel")!;

        Assert.Equal(StatType.Con, channel.Roll!.Stat);
        Assert.Contains("roll +CON", channel.Summary);
        Assert.Contains("**10+:**", channel.Summary);
        Assert.Contains("• It causes a blast wave", channel.Summary);
    }

    [Fact]
    public void AdvancedMoves_AreLockedBelowTheirLevel()
    {
        var sheet = Sheet(ClassType.Battlemaster);

        Assert.Equal("Phase 2 needs level 2.", sheet.WhyCannotLearn(sheet.FindMove("Phase 2")!, 1, []));
        Assert.Equal("Lethal Gambit needs level 6.", sheet.WhyCannotLearn(sheet.FindMove("Lethal Gambit")!, 5, []));
        Assert.Null(sheet.WhyCannotLearn(sheet.FindMove("Lethal Gambit")!, 6, []));
    }

    [Fact]
    public void AdvancedMoves_OnePerLevelGained()
    {
        var sheet = Sheet(ClassType.Battlemaster);

        Assert.Null(sheet.WhyCannotLearn(sheet.FindMove("Phase 2")!, 2, []));
        Assert.NotNull(sheet.WhyCannotLearn(sheet.FindMove("Phase 2")!, 2, ["Risky Gambit"]));
        Assert.Null(sheet.WhyCannotLearn(sheet.FindMove("Phase 2")!, 3, ["Risky Gambit"]));
    }

    [Fact]
    public void AdvancedMoves_NeedTheirRequirement()
    {
        var sheet = Sheet(ClassType.Necromancer);

        Assert.Equal("Colossus requires Unliving Large.", sheet.WhyCannotLearn(sheet.FindMove("Colossus")!, 6, []));
        Assert.Null(sheet.WhyCannotLearn(sheet.FindMove("Colossus")!, 6, ["unliving large"]));
    }

    [Fact]
    public void AlreadyKnownMoves_CantBeLearnedAgain()
    {
        var sheet = Sheet(ClassType.Slayer);

        Assert.Equal("You already have Ensnare.", sheet.WhyCannotLearn(sheet.FindMove("Ensnare")!, 5, ["ensnare"]));
    }

    [Fact]
    public void Features_AllowOnePickUnlessAMoveGrantsMore()
    {
        var sheet = Sheet(ClassType.Slayer);
        var tenacity = sheet.FindMove("Tenacity")!;

        Assert.Null(sheet.WhyCannotLearn(tenacity, 1, []));
        Assert.Equal("You've already chosen your Manifestation.", sheet.WhyCannotLearn(tenacity, 1, ["Adrenaline"]));
        Assert.Null(sheet.WhyCannotLearn(tenacity, 2, ["Adrenaline", "Useful Symptoms"]));
        Assert.NotNull(sheet.WhyCannotLearn(tenacity, 2, ["Adrenaline", "Deadly Throw", "Useful Symptoms"]));
    }

    [Fact]
    public void Features_DontCountAsAdvancedMoves()
    {
        var sheet = Sheet(ClassType.Slayer);

        Assert.Null(sheet.WhyCannotLearn(sheet.FindMove("Tenacity")!, 1, []));
        Assert.Empty(sheet.AvailableAdvancedMoves(1, []));
    }

    [Fact]
    public void FirstSpirits_NeedTheGrandTotem()
    {
        var sheet = Sheet(ClassType.Shaman);
        var winter = sheet.FindMove("Spirit of the First Winter")!;

        Assert.Equal(MoveTier.Extra, winter.Tier);
        Assert.NotNull(sheet.WhyCannotLearn(winter, 1, []));
        Assert.Null(sheet.WhyCannotLearn(winter, 1, ["Totem of the First Spirits"]));
        Assert.Null(sheet.WhyCannotLearn(sheet.FindMove("Spirit of Mending")!, 1, []));
    }

    [Fact]
    public void Resources_GrantedByAMove_AppearOnceKnown()
    {
        var sheet = Sheet(ClassType.Templar);

        Assert.Equal(["Wrath"], sheet.ResourcesFor([]).Select(r => r.Name));
        Assert.Equal(["Wrath", "Prophecy"], sheet.ResourcesFor(["Prophetic Dream"]).Select(r => r.Name));
    }

    [Fact]
    public void Build_GrantsStartingMovesAndChoices()
    {
        var draft = new CharacterDraft { Name = "Varro" };
        draft.SetClass(ClassKits.All.Single(k => k.Type == ClassType.Battlemaster), new DiceRoller());
        var tactic = draft.Sheet!.Features.Single();
        draft.FeatureChoices[tactic.Name] = [tactic.Options[1]];

        var abilities = draft.Build(new Snowflake(1)).Abilities!.Select(a => a.Name).ToList();

        Assert.Contains("Gambit", abilities);
        Assert.Contains("Change Tactics", abilities);
        Assert.Contains("Death: Grand Plan", abilities);
        Assert.Contains("Default Tactic: Cautious", abilities);
        Assert.DoesNotContain("Phase 2", abilities);
    }

    [Fact]
    public void ChangingClass_ClearsChoices()
    {
        var draft = new CharacterDraft();
        draft.SetClass(ClassKits.All.Single(k => k.Type == ClassType.Slayer), new DiceRoller());
        draft.FeatureChoices["Manifestation"] = [draft.Sheet!.FindMove("Tenacity")!];

        draft.SetClass(ClassKits.All.Single(k => k.Type == ClassType.Ranger), new DiceRoller());

        Assert.Empty(draft.FeatureChoices);
        var abilities = draft.Build(new Snowflake(1)).Abilities!.Select(a => a.Name).ToList();
        Assert.Contains("Hunt and Track", abilities);
        Assert.DoesNotContain("Tenacity", abilities);
    }

    [Fact]
    public void EveryClassHasASheet()
    {
        Assert.All(Enum.GetValues<ClassType>(), t => Assert.NotNull(ClassSheetLibrary.For(t)));
    }

    [Fact]
    public void Build_AppliesArmorAndDamageFromChoices()
    {
        var golem = new CharacterDraft();
        golem.SetClass(ClassKits.All.Single(k => k.Type == ClassType.Golem), new DiceRoller());
        golem.FeatureChoices["Composition"] = [golem.Sheet!.FindMove("Composition: Solid")!];
        golem.FeatureChoices["Built for Battle"] = [golem.Sheet.FindMove("Built for Battle: Armored")!, golem.Sheet.FindMove("Built for Battle: Violent")!];
        Assert.Equal(3, golem.Build(new Snowflake(1)).ArmorRating);

        var dancer = new CharacterDraft();
        dancer.SetClass(ClassKits.All.Single(k => k.Type == ClassType.SkyDancer), new DiceRoller());
        Assert.Null(dancer.Build(new Snowflake(1)).DamageModifier);
        dancer.FeatureChoices["Means of Flight"] = [dancer.Sheet!.FindMove("Flight: Natural")!];
        Assert.Equal("d8", dancer.Build(new Snowflake(1)).DamageModifier);
    }

    [Fact]
    public void Requires_AcceptsAnyOfSeveralMoves()
    {
        var sheet = Sheet(ClassType.Captain);
        var legendary = sheet.FindMove("Legendary Ship")!;

        Assert.NotNull(sheet.WhyCannotLearn(legendary, 6, ["Take Charge"]));
        Assert.Null(sheet.WhyCannotLearn(legendary, 6, ["Hell and Thunder"]));
        Assert.Null(sheet.WhyCannotLearn(legendary, 6, ["Renovations"]));
    }

    [Fact]
    public void MultiPickFeatures_CountExtraPicks()
    {
        var sheet = Sheet(ClassType.Survivor);
        var marks = sheet.Features.Single(f => f.Name == "Eternal Mark");

        Assert.Equal(2, marks.AllowedPicks([]));
        Assert.Equal(3, marks.AllowedPicks(["Cataclysm: God"]));
        Assert.NotNull(sheet.WhyCannotLearn(sheet.FindMove("A Scar Burning")!, 1, ["A Hand Bloodied", "A Body Scarred"]));
        Assert.Null(sheet.WhyCannotLearn(sheet.FindMove("A Scar Burning")!, 1, ["A Hand Bloodied", "A Body Scarred", "Cataclysm: God"]));
    }

    [Fact]
    public void GolemForceMoves_TheOtherCanBeTakenAsAnAdvance()
    {
        var sheet = Sheet(ClassType.Golem);
        var unstoppable = sheet.FindMove("Unstoppable Force")!;

        Assert.NotNull(sheet.WhyCannotLearn(unstoppable, 1, ["Immovable Object"]));
        Assert.Null(sheet.WhyCannotLearn(unstoppable, 2, ["Immovable Object"]));
        Assert.Equal(1, sheet.AdvancesTaken(["Immovable Object", "Unstoppable Force"]));
        Assert.NotNull(sheet.WhyCannotLearn(sheet.FindMove("Arcane Void")!, 2, ["Immovable Object", "Unstoppable Force"]));
    }

    [Fact]
    public void ResourceBonuses_RaiseTheMax()
    {
        var stress = Sheet(ClassType.Captain).FindResource("Ship Stress")!;

        Assert.Equal(3, stress.MaxFor([]));
        Assert.Equal(6, stress.MaxFor(["Ship: Sturdy", "Ship: Tank"]));
    }

    [Fact]
    public void ClassStat_ComesFromTheFirstPickedOption()
    {
        var sheet = Sheet(ClassType.Collector);
        var character = new Character
        {
            Abilities =
            [
                new Ability { ID = 1, Name = "Predilection: Mundane" },
                new Ability { ID = 2, Name = "Predilection: Living" },
            ],
        };

        Assert.Equal(StatType.Int, ClassSheetLibrary.ResolveClassStat(character, sheet));
        Assert.Null(ClassSheetLibrary.ResolveClassStat(new Character { Abilities = [] }, sheet));
    }

    [Fact]
    public async Task Autocomplete_FiltersAndStaysWithinDiscordLimits()
    {
        var suggestions = await new ClassMoveAutocomplete().GetSuggestionsAsync([], "spirit");

        Assert.InRange(suggestions.Count, 1, 25);
        Assert.All(suggestions, s =>
        {
            Assert.Contains("spirit", s.Name, StringComparison.OrdinalIgnoreCase);
            Assert.True(s.Name.Length <= 100);
        });

        var rolled = await new RolledClassMoveAutocomplete().GetSuggestionsAsync([], "ra");
        Assert.Contains(rolled, s => s.Value.AsT0 == "Raise the Dead");
        Assert.DoesNotContain(rolled, s => s.Value.AsT0 == "Trickle");
        Assert.Contains(rolled, s => s.Name == "Raise the Dead (Necromancer)");
    }
}

public sealed class ResourceServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ResourceServiceTests()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    private DungeonWorldContext CreateContext() =>
        new(new DbContextOptionsBuilder<DungeonWorldContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Resources_StartAtDefaultAndStayInRange()
    {
        var wrath = ClassSheetLibrary.For(ClassType.Templar)!.FindResource("wrath")!;
        var userId = new Snowflake(42);

        await using (var context = CreateContext())
        {
            var character = new Character
            {
                ID = userId,
                Name = "Ilse",
                Class = new Class { Type = ClassType.Templar },
                Health = new Health { CurrentHP = 10, MaxHP = 10 },
                Inventory = new Inventory(),
                Stats = []
            };
            await new CharacterService(context).AddCharacterAsync(character);

            var resources = new ResourceService(context);
            Assert.Equal(3, resources.GetValue(character, wrath));
            Assert.Equal(3, await resources.SetValueAsync(character, wrath, 7));
            Assert.Equal(0, await resources.SetValueAsync(character, wrath, -2));
            Assert.Equal(1, await resources.SetValueAsync(character, wrath, 1));
        }

        await using (var context = CreateContext())
        {
            var character = await new CharacterService(context).GetCharacterFromUserAsync(userId);
            Assert.Equal(1, new ResourceService(context).GetValue(character!, wrath));
        }
    }
}
