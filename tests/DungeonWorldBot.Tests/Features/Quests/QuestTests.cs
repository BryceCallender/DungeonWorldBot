using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Quests;
using DungeonWorldBot.Rules;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Quests;

public sealed class QuestTests : IDisposable
{
    private static readonly Snowflake UserId = new(55);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DungeonWorldContext CreateContext() => _db.CreateContext();

    private static async Task<Character> AddPaladinAsync(DungeonWorldContext context, params string[] moves)
    {
        var character = new Character
        {
            ID = UserId,
            Name = "Arthur",
            Class = new Class { Type = ClassType.Paladin },
            Health = new Health { CurrentHP = 20, MaxHP = 20 },
            Inventory = new Inventory(),
            Stats = [],
            Abilities = new[] { new Ability { Name = QuestRules.Quest, Description = QuestRules.QuestMove.Summary } }
                .Concat(moves.Select(m => new Ability { Name = m }))
                .ToList(),
        };
        await new CharacterService(context).AddCharacterAsync(character);
        return character;
    }

    private static QuestService ServiceFor(DungeonWorldContext context) =>
        new(new AbilityService(context), new ArmorService(context));

    [Fact]
    public async Task DeclaringAQuest_SavesTheMissionBoonsAndVows()
    {
        await using var context = CreateContext();
        var character = await AddPaladinAsync(context);
        var service = ServiceFor(context);
        var invulnerability = QuestRules.FindBoon("invulnerability")!;

        Assert.False(QuestRules.IsOnQuest(character));
        await service.DeclareAsync(character, " Slay the Wyrm of Ashfall ", [new QuestBoonChoice(invulnerability, "fire")]);
        await service.AddVowAsync(character, "Valor");

        await using var reloaded = CreateContext();
        var saved = (await new CharacterService(reloaded).GetCharacterFromUserAsync(UserId))!;

        Assert.Equal("Slay the Wyrm of Ashfall", QuestRules.Mission(saved));
        Assert.Equal("Invulnerability to fire.", Assert.Single(QuestRules.BoonsOf(saved)).Description);
        Assert.Equal("Vow: Valor", Assert.Single(QuestRules.VowsOf(saved)).Name);
    }

    [Fact]
    public async Task Smite_AndHolyProtection_OnlyWorkOnAQuest()
    {
        await using var context = CreateContext();
        var character = await AddPaladinAsync(context, QuestRules.Smite, QuestRules.HolyProtection);
        var service = ServiceFor(context);

        Assert.Null(QuestRules.SmiteDamage(character));

        await service.DeclareAsync(character, "Defend the village", []);
        Assert.Equal("1d4", QuestRules.SmiteDamage(character));
        Assert.Contains(character.ArmorBonuses, b => b.Source == QuestRules.ArmorSource && b.Amount == 1);

        await service.EndAsync(character);
        Assert.Null(QuestRules.SmiteDamage(character));
        Assert.Empty(character.ArmorBonuses);
        Assert.Equal(QuestRules.QuestMove.Summary, QuestRules.QuestAbility(character)!.Description);
    }

    [Fact]
    public async Task EndingAQuest_ClearsItsBoonsAndVows()
    {
        await using var context = CreateContext();
        var character = await AddPaladinAsync(context);
        var service = ServiceFor(context);

        await service.DeclareAsync(character, "Discover the truth", [new QuestBoonChoice(QuestRules.FindBoon("Piercing Senses")!, null)]);
        await service.AddVowAsync(character, "Truth");
        await service.EndAsync(character);

        Assert.False(QuestRules.IsOnQuest(character));
        Assert.Empty(QuestRules.BoonsOf(character));
        Assert.Empty(QuestRules.VowsOf(character));
    }

    [Fact]
    public async Task HolySmite_AndPerfectKnight_RaiseTheirNumbers()
    {
        await using var context = CreateContext();
        var character = await AddPaladinAsync(context, QuestRules.HolySmite, QuestRules.PerfectKnight);

        await ServiceFor(context).DeclareAsync(character, "Slay the blight", []);

        Assert.Equal("1d8", QuestRules.SmiteDamage(character));
        Assert.Equal(QuestRules.PerfectKnightBoons, QuestRules.MaxBoons(character));
    }
}
