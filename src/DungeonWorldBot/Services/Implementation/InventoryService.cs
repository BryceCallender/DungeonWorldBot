﻿using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.VoiceGateway;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DungeonWorldBot.Services.Implementation
{
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
}
