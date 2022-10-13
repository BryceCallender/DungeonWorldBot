using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.VoiceGateway;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DungeonWorldBot.Services.Implementation
{
    public class ItemService
    {
        public static void setItem(ref Item item, string name, int amount, int weight)
        {
            item.Name = name;
            item.Amount = amount;
            item.Weight = weight;
        }
    }
}
