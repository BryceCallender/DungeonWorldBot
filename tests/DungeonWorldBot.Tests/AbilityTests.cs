using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Services.Implementation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests;

public sealed class AbilityTests : IDisposable
{
    private static readonly Snowflake UserId = new(123456789012345678);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public AbilityTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    private DungeonWorldContext CreateContext() =>
        new(new DbContextOptionsBuilder<DungeonWorldContext>().UseSqlite(_connection).Options);

    private async Task<Character> AddCharacterAsync(DungeonWorldContext context)
    {
        var character = new Character
        {
            ID = UserId,
            Name = "Morrow",
            Class = new Class { Type = ClassType.Necromancer },
            Health = new Health { CurrentHP = 10, MaxHP = 10 },
            Inventory = new Inventory(),
            Stats = []
        };

        await new CharacterService(context).AddCharacterAsync(character);
        return character;
    }

    [Fact]
    public async Task Abilities_RoundTripThroughSqlite()
    {
        await using (var context = CreateContext())
        {
            var character = await AddCharacterAsync(context);
            var abilities = new AbilityService(context);
            await abilities.AddAbilityAsync(character, " Raise Dead ", "Animate a corpse", maxUses: 3);
            await abilities.AddAbilityAsync(character, "Grave Sense", null, maxUses: null);
        }

        await using (var context = CreateContext())
        {
            var character = await new CharacterService(context).GetCharacterFromUserAsync(UserId);

            Assert.NotNull(character);
            Assert.Equal(2, character.Abilities!.Count);

            var raiseDead = new AbilityService(context).FindAbility(character, "raise dead");
            Assert.NotNull(raiseDead);
            Assert.Equal("Raise Dead", raiseDead.Name);
            Assert.Equal(3, raiseDead.Uses);
            Assert.Equal("Raise Dead (3/3)", raiseDead.ToDisplay());
        }
    }

    [Fact]
    public async Task Use_SpendsUsesAndRefusesWhenOut()
    {
        await using var context = CreateContext();
        var character = await AddCharacterAsync(context);
        var abilities = new AbilityService(context);
        var ability = await abilities.AddAbilityAsync(character, "Raise Dead", null, maxUses: 2);

        Assert.True(await abilities.UseAbilityAsync(ability, 2));
        Assert.Equal(0, ability.Uses);
        Assert.False(await abilities.UseAbilityAsync(ability, 1));
        Assert.Equal(0, ability.Uses);
    }

    [Fact]
    public async Task Use_UnlimitedAbilityAlwaysSucceeds()
    {
        await using var context = CreateContext();
        var character = await AddCharacterAsync(context);
        var abilities = new AbilityService(context);
        var ability = await abilities.AddAbilityAsync(character, "Grave Sense", null, maxUses: null);

        Assert.True(await abilities.UseAbilityAsync(ability, 5));
        Assert.Equal("Grave Sense", ability.ToDisplay());
    }

    [Fact]
    public async Task Restore_CapsAtMaxButKeepsExtraHold()
    {
        await using var context = CreateContext();
        var character = await AddCharacterAsync(context);
        var abilities = new AbilityService(context);
        var ability = await abilities.AddAbilityAsync(character, "Defend", null, maxUses: 3);

        await abilities.UseAbilityAsync(ability, 3);
        await abilities.RestoreAbilityAsync(ability, 5);
        Assert.Equal(3, ability.Uses);

        await abilities.SetAbilityUsesAsync(ability, 5);
        await abilities.RestoreAllAbilitiesAsync(character);
        Assert.Equal(5, ability.Uses);

        await abilities.SetAbilityUsesAsync(ability, 0);
        await abilities.RestoreAbilityAsync(ability);
        Assert.Equal(3, ability.Uses);
    }

    [Fact]
    public async Task Remove_DeletesAbility()
    {
        await using (var context = CreateContext())
        {
            var character = await AddCharacterAsync(context);
            var abilities = new AbilityService(context);
            var ability = await abilities.AddAbilityAsync(character, "Raise Dead", null, maxUses: null);
            await abilities.RemoveAbilityAsync(character, ability);
        }

        await using (var context = CreateContext())
        {
            Assert.Empty(context.Set<Ability>());
        }
    }
}
