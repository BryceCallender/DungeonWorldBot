using DSharpPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Remora.Discord.API.Objects;
using Remora.Discord.Gateway;
using Remora.Discord.Commands.Feedback.Services;
using DSharpPlus.Entities;
using Remora.Rest.Core;
using Remora.Discord.API.Abstractions.Objects;
using DungeonWorldBot.Services.Interactivity;

namespace DungeonWorldBot.Services
{
    public interface IDialogueStep
    {
        Action<DiscordMessage> OnMessageAdded { get; set; }
        IDialogueStep NextStep { get; }
        Task<bool> ProcessStep(InteractivityService interactivity, Snowflake channel, IUser user);
    }
}
