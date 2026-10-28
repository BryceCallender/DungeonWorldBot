using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters.Creation;
using DungeonWorldBot.Features.Rolls;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Characters.Creation;

public class CharacterCreationTests
{
    private readonly IDiceRoller _dice = new DiceRoller();

    private static ClassKit Kit(ClassType type) => ClassKits.All.Single(k => k.Type == type);

    private CharacterDraft DraftWithChoices(ClassType type, params ReadOnlySpan<int> choices)
    {
        var draft = new CharacterDraft();
        draft.SetClass(Kit(type), _dice);

        for (var i = 0; i < choices.Length; i++)
        {
            draft.ApplyChoice(draft.Kit.Questions[i].Choices[choices[i]], _dice);
        }

        return draft;
    }

    [Fact]
    public void EveryClassHasExactlyOneKit()
    {
        Assert.Equal(Enum.GetValues<ClassType>().Order(), ClassKits.All.Select(k => k.Type).Order());
    }

    [Fact]
    public void EveryKitQuestionOffersAChoice()
    {
        Assert.All(ClassKits.All.SelectMany(k => k.Questions), q => Assert.True(q.Choices.Count >= 2, q.Prompt));
    }

    [Fact]
    public void KitItemsHaveNoWeightWhenWeightIsNotTracked()
    {
        // The Necromancer's kit has weighted items, including the hexed jars
        var draft = new CharacterDraft { TrackWeight = false };
        draft.SetClass(Kit(ClassType.Necromancer), _dice);
        foreach (var question in draft.Kit.Questions)
            draft.ApplyChoice(question.Choices[0], _dice);

        Assert.All(draft.Items, i => Assert.Equal(0, i.Weight));
    }

    [Fact]
    public void KitItemsKeepTheirWeightWhenWeightIsTracked()
    {
        var draft = new CharacterDraft();
        draft.SetClass(Kit(ClassType.Necromancer), _dice);
        foreach (var question in draft.Kit.Questions)
            draft.ApplyChoice(question.Choices[0], _dice);

        Assert.Contains(draft.Items, i => i.Weight > 0);
    }

    [Fact]
    public void EveryAlignmentCanBePicked()
    {
        var pickable = CharacterDraft.AlignmentOptions.Select(o => o.Value).ToHashSet();
        Assert.All(Enum.GetValues<Alignment>().Where(a => a != Alignment.Unknown), a => Assert.Contains(a, pickable));
    }

    [Theory]
    [InlineData(ClassType.Battlemaster, new[] { 0, 0, 0 }, 3, 0)] // scale armor + shield
    [InlineData(ClassType.Battlemaster, new[] { 1, 1, 0 }, 1, 1)] // chainmail + halberd
    [InlineData(ClassType.Paladin, new[] { 1, 0 }, 3, 0)]         // base scale armor + shield
    [InlineData(ClassType.Druid, new[] { 0, 0, 0 }, 1, 0)]
    [InlineData(ClassType.Channeler, new[] { 1, 0, 0 }, 0, 0)]
    public void Choices_AddArmorAndDamage(ClassType type, int[] choices, int armor, int damage)
    {
        var draft = DraftWithChoices(type, choices);

        Assert.Equal(armor, draft.ArmorRating);
        Assert.Equal(damage, draft.DamageRating);
    }

    [Fact]
    public void Ranger_ArrowChoice_StacksArrows()
    {
        var draft = DraftWithChoices(ClassType.Ranger, 0, 0);

        Assert.Equal(6, draft.Items.Single(i => i.Name == "Bundle of Arrows").Amount);
        Assert.Equal(5, draft.Items.Single(i => i.Name == "Dungeon Rations").Amount);
    }

    [Fact]
    public void Ranger_RationsChoice_StacksRations()
    {
        var draft = DraftWithChoices(ClassType.Ranger, 0, 1);

        Assert.Equal(3, draft.Items.Single(i => i.Name == "Bundle of Arrows").Amount);
        Assert.Equal(10, draft.Items.Single(i => i.Name == "Dungeon Rations").Amount);
    }

    [Fact]
    public void Channeler_CoinPouch_RollsOneToTen()
    {
        for (var i = 0; i < 100; i++)
        {
            var draft = DraftWithChoices(ClassType.Channeler, 0, 0, 1);
            Assert.InRange(draft.Items.Single(item => item.Name == "Coins").Amount, 1, 10);
        }
    }

    [Fact]
    public void Necromancer_JarsHoldChosenCorpse()
    {
        var draft = DraftWithChoices(ClassType.Necromancer, 0, 0, 2);

        var jars = Assert.IsType<HexedJars>(draft.Items.Single(i => i.Name == "Hexed Canopic Jars"));
        Assert.Equal(["A Skinless Hound", "Empty", "Empty"], jars.Contents);
    }

    [Fact]
    public void ChangingClass_ReplacesGear()
    {
        var draft = DraftWithChoices(ClassType.Battlemaster, 0, 0, 0);

        draft.SetClass(Kit(ClassType.Druid), _dice);

        Assert.Equal(0, draft.ArmorRating);
        Assert.Equal(["Token of Your Land"], draft.Items.Select(i => i.Name));
    }

    [Fact]
    public void ResettingGear_DoesNotKeepPreviousChoices()
    {
        var draft = DraftWithChoices(ClassType.Paladin, 1, 1);

        draft.ResetGear(_dice);

        Assert.Equal(2, draft.ArmorRating);
        Assert.Equal(5, draft.Items.Single(i => i.Name == "Dungeon Rations").Amount);
        Assert.DoesNotContain(draft.Items, i => i.Name == "Shield");
    }

    [Fact]
    public void Build_SetsHealthFromConstitutionAndClass()
    {
        var draft = DraftWithChoices(ClassType.Paladin, 0, 0);
        draft.Name = "Aria";
        foreach (var (type, value) in CharacterDraft.StatOrder.Zip(CharacterDraft.StatArray))
        {
            draft.Stats[type] = value;
        }

        var character = draft.Build(new Snowflake(42));

        Assert.Equal(new Snowflake(42), character.ID);
        Assert.Equal(12 + 10, character.Health.MaxHP); // Con 12 + Paladin 10
        Assert.Equal(character.Health.MaxHP, character.Health.CurrentHP);
        Assert.Equal(CharacterDraft.StatOrder, character.Stats.Select(s => s.StatType));
        Assert.Equal(draft.Items.Count, character.Inventory.Items!.Count);
    }
}
