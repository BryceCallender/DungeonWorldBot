using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Spells;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Tests.Features.Spells;

public class SpellTests
{
    private static Spell Spell(SpellList list, string name) => SpellLists.Find($"{name} ({list})").Single();

    [Fact]
    public void SpellsFitInDiscord()
    {
        Assert.All(SpellLists.All, s =>
        {
            Assert.InRange(s.Name.Length + s.Heading.Length + 3, 1, 256);
            Assert.InRange(s.Description.Length, 1, 1024);
        });
    }

    [Theory]
    [InlineData(SpellList.Cleric, 0, 3)]
    [InlineData(SpellList.Cleric, 1, 7)]
    [InlineData(SpellList.Wizard, 0, 3)]
    [InlineData(SpellList.Wizard, 9, 5)]
    public void ListsHaveEverySpell(SpellList list, int level, int count)
    {
        Assert.Equal(count, SpellLists.For(list).Count(s => s.Level == level));
    }

    [Fact]
    public void SharedNames_ResolveByList()
    {
        Assert.Equal(2, SpellLists.Find("Light").Count);
        Assert.Equal(SpellList.Wizard, SpellLists.Find("light (wizard)").Single().List);
    }

    [Fact]
    public void Learning_RespectsCasterLevelAndTotal()
    {
        List<Spell> known = [Spell(SpellList.Cleric, "Cure Light Wounds"), Spell(SpellList.Cleric, "Magic Weapon")];

        // Caster level 1 allows 2 spell levels, and both are used
        Assert.Contains("2 of 2", SpellCasting.WhyCannotLearn(Spell(SpellList.Cleric, "Sanctuary"), 1, known));
        Assert.Null(SpellCasting.WhyCannotLearn(Spell(SpellList.Cleric, "Sanctuary"), 2, known));
        Assert.Contains("above your cleric level", SpellCasting.WhyCannotLearn(Spell(SpellList.Cleric, "Darkness"), 2, []));
        Assert.Null(SpellCasting.WhyCannotLearn(Spell(SpellList.Cleric, "Guidance"), 1, known));
        Assert.Contains("already have", SpellCasting.WhyCannotLearn(known[0], 5, known));
    }

    [Fact]
    public void GodAmidstTheWastes_GivesClericSpells()
    {
        var ranger = new Character
        {
            Class = new Class { Type = ClassType.Ranger },
            Abilities = [new Ability { ID = 1, Name = "God Amidst the Wastes" }],
        };

        var access = Assert.Single(SpellCasting.Access(ranger));
        Assert.Equal(SpellList.Cleric, access.List);
        Assert.Equal("Cleric Level", access.LevelResource.Name);

        Assert.Empty(SpellCasting.Access(new Character { Class = new Class { Type = ClassType.Ranger }, Abilities = [] }));
    }

    [Fact]
    public void KnownSpells_UseTheListNote()
    {
        var character = new Character
        {
            Abilities =
            [
                new Ability { Name = "Light", Description = "...\n" + SpellCasting.ListNote(SpellList.Wizard) },
                new Ability { Name = "Bless", Description = "something unrelated" },
            ],
        };

        Assert.Equal(SpellList.Wizard, Assert.Single(SpellCasting.KnownSpells(character)).List);
    }
}
