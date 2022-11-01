using DungeonWorldBot.API.Models;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;

namespace DungeonWorldBot.Services.Implementation;

public interface IInventoryService
{
    Task SaveInventoryAsync();
}