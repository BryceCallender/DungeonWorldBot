using DungeonWorldBot.Data;
using DungeonWorldBot.Infrastructure;

namespace DungeonWorldBot.Features.Items;

[Scoped<IInventoryService>]
public class InventoryService : IInventoryService
{
    private readonly DungeonWorldContext _dungeonWorldContext;

    public InventoryService(DungeonWorldContext dungeonWorldContext)
    {
        _dungeonWorldContext = dungeonWorldContext;
    }

    public async Task SaveInventoryAsync()
    {
        await _dungeonWorldContext.SaveChangesAsync();
    }
}
