using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Characters.Creation;
using DungeonWorldBot.Features.Rolls;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Characters;

public sealed class DatabaseTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DungeonWorldContext CreateContext() => _db.CreateContext();

    [Fact]
    public async Task CreatedCharacter_RoundTripsThroughSqlite()
    {
        var draft = new CharacterDraft { Name = "Morrow", Race = Race.Human, Alignment = Alignment.NeutralEvil };
        draft.SetClass(ClassKits.All.Single(k => k.Type == ClassType.Necromancer), new DiceRoller());
        for (var i = 0; i < draft.Kit.Questions.Count; i++)
        {
            draft.ChooseGear(i, [draft.Kit.Questions[i].Choices[^1]], new DiceRoller());
        }
        foreach (var (type, value) in CharacterDraft.StatOrder.Zip(CharacterDraft.StatArray))
        {
            draft.Stats[type] = value;
        }

        var userId = new Snowflake(123456789012345678);

        await using (var context = CreateContext())
        {
            await new CharacterService(context).AddCharacterAsync(draft.Build(userId));
        }

        await using (var context = CreateContext())
        {
            var service = new CharacterService(context);
            var character = await service.GetCharacterFromUserAsync(userId);

            Assert.NotNull(character);
            Assert.Equal("Morrow", character.Name);
            Assert.Equal(Alignment.NeutralEvil, character.Alignment);
            Assert.Equal(ClassType.Necromancer, character.Class.Type);
            Assert.Equal(6, character.Stats.Count);
            Assert.Contains(character.Inventory.Items, i => i.Name == "Hexed Canopic Jars");

            await service.DamageAsync(character, 100);
            Assert.Equal(0, character.Health.CurrentHP);
        }
    }

    // New inventories all have an ID of 0 until they're saved, so noticing what changed can't tell them apart by it
    [Fact]
    public async Task SeveralCharacters_CanBeMadeInOneSave()
    {
        await using var context = CreateContext();
        Character Hero(ulong id, string name, string item) => new()
        {
            ID = new Snowflake(id),
            Name = name,
            Class = new Class { Type = ClassType.Fighter },
            Health = new Health { CurrentHP = 10, MaxHP = 10 },
            Inventory = new Inventory { Items = [new Item { Name = item }] },
        };
        context.Characters.AddRange(Hero(1, "Aria", "Rope"), Hero(2, "Bram", "Lantern"));

        await context.SaveChangesAsync();

        // Each gets the one event for being made, not one for everything they start with
        var events = await context.CharacterEvents.OrderBy(e => e.CharacterID).ToListAsync();
        Assert.Equal([new Snowflake(1), new Snowflake(2)], events.Select(e => e.CharacterID));
        Assert.All(events, e => Assert.Equal(CharacterEventKind.Created, e.Kind));
    }
}
