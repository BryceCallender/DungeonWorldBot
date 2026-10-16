using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Resources;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Resources;

public sealed class ResourceServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DungeonWorldContext CreateContext() => _db.CreateContext();

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
