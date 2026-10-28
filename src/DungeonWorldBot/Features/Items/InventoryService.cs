using DungeonWorldBot.Data;
using DungeonWorldBot.Infrastructure;

namespace DungeonWorldBot.Features.Items;

[Scoped<IInventoryService>]
public class InventoryService(DungeonWorldContext dungeonWorldContext) : IInventoryService
{
    public async Task SaveInventoryAsync()
    {
        await dungeonWorldContext.SaveChangesAsync();
    }
}
