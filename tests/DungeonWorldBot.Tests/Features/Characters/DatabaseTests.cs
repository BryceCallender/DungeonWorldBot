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
        foreach (var question in draft.Kit.Questions)
        {
            draft.ApplyChoice(question.Choices[^1], new DiceRoller());
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
            Assert.Contains(character.Inventory.Items!, i => i.Name == "Hexed Canopic Jars");

            await service.DamageAsync(character, 100);
            Assert.Equal(0, character.Health.CurrentHP);
        }
    }
}
