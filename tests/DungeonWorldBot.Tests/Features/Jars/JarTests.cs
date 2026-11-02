using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Jars;
using DungeonWorldBot.Rules;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Jars;

public sealed class JarTests : IDisposable
{
    private static readonly Snowflake UserId = new(66);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DungeonWorldContext CreateContext() => _db.CreateContext();

    private static async Task<Character> AddNecromancerAsync(DungeonWorldContext context, params Item[] items)
    {
        var character = new Character
        {
            ID = UserId,
            Name = "Mortis",
            Class = new Class { Type = ClassType.Necromancer },
            Health = new Health { CurrentHP = 16, MaxHP = 16 },
            Inventory = new Inventory { Items = items.ToList() },
            Stats = [],
            Abilities = [new Ability { Name = JarRules.CorpseCollector }],
        };
        await new CharacterService(context).AddCharacterAsync(character);
        return character;
    }

    private async Task<Character> ReloadAsync()
    {
        var reloaded = CreateContext();
        return (await new CharacterService(reloaded).GetCharacterFromUserAsync(UserId))!;
    }

    [Fact]
    public async Task ItemKinds_KeepTheirDetailsWhenSaved()
    {
        await using var context = CreateContext();
        await AddNecromancerAsync(context,
            new HexedJars { Name = JarRules.JarsName, Amount = 3, Contents = ["A Skinless Hound", JarRules.Empty, JarRules.Empty] },
            new Weapon { Name = "Bone Dagger", Damage = 1 },
            new Armor { Name = "Leather", ArmorRating = 1, Type = ArmorType.Physical });

        var saved = await ReloadAsync();

        Assert.Equal(["A Skinless Hound"], JarRules.Corpses(saved));
        Assert.Equal(1, saved.Inventory.Items!.OfType<Weapon>().Single().Damage);
        Assert.Equal(1, saved.Inventory.Items!.OfType<Armor>().Single().ArmorRating);
    }

    [Fact]
    public async Task StoringAndOpening_FillsAndEmptiesJars()
    {
        await using var context = CreateContext();
        var character = await AddNecromancerAsync(context,
            new HexedJars { Name = JarRules.JarsName, Amount = 3, Contents = ["A Skinless Hound", JarRules.Empty, JarRules.Empty] });
        var service = new JarService(context);

        await service.StoreAsync(character, " Bandit ");
        await service.EmptyAsync(character, "a skinless hound");

        var saved = await ReloadAsync();
        Assert.Equal(["Bandit", JarRules.Empty, JarRules.Empty], JarRules.Slots(saved));
    }

    [Fact]
    public async Task FullJars_RefuseAnotherCorpse_UntilTrueCollectorAddsOne()
    {
        await using var context = CreateContext();
        var character = await AddNecromancerAsync(context);
        var service = new JarService(context);
        foreach (var corpse in new[] { "One", "Two", "Three" })
            await service.StoreAsync(character, corpse);

        Assert.NotNull(JarRules.WhyCannotStore(character, "Four"));

        character.Abilities!.Add(new Ability { Name = JarRules.TrueCollector });
        Assert.Null(JarRules.WhyCannotStore(character, "Four"));
    }

    [Fact]
    public async Task StoringWithoutJars_GivesThemBack()
    {
        await using var context = CreateContext();
        var character = await AddNecromancerAsync(context);

        await new JarService(context).StoreAsync(character, "Goblin");

        var saved = await ReloadAsync();
        Assert.Equal(JarRules.BaseJars, JarRules.Jars(saved)!.Amount);
        Assert.Equal(["Goblin"], JarRules.Corpses(saved));
    }
}
