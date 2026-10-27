using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Gear;

public sealed class InventoryTests : IDisposable
{
    private static readonly Snowflake UserId = new(77);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task RemovingAnItem_DeletesIt()
    {
        await using (var context = _db.CreateContext())
        {
            await new CharacterService(context).AddCharacterAsync(new Character
            {
                ID = UserId,
                Name = "Selene",
                Class = new Class { Type = ClassType.Ranger },
                Health = new Health { CurrentHP = 20, MaxHP = 20 },
                Inventory = new Inventory { Items = [new Item { Name = "Rope" }, new Item { Name = "Torch" }] },
                Stats = [],
                Abilities = [],
            });
        }

        // As /inventory remove does it: taken out of the list, then saved
        await using (var context = _db.CreateContext())
        {
            var character = (await new CharacterService(context).GetCharacterFromUserAsync(UserId))!;
            character.Inventory.Items!.RemoveAll(i => i.Name == "Rope");
            await context.SaveChangesAsync();
        }

        // Gone from the database, not left behind with no inventory
        await using (var context = _db.CreateContext())
            Assert.Equal(["Torch"], await context.Set<Item>().Select(i => i.Name).ToListAsync());
    }
}
