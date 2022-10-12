using DungeonWorldBot.API.Models;
using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Services;

public interface IRollService
{
    Roll Roll(string roll);

    Roll RollWithStat(string roll, Stat stat);
}