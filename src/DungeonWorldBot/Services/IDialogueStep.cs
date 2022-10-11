using Remora.Discord.API.Objects;
using Remora.Rest.Core;
using Remora.Discord.API.Abstractions.Objects;
using DungeonWorldBot.Services.Interactivity;

namespace DungeonWorldBot.Services
{
    public interface IDialogueStep
    {
        Action<Message> OnMessageAdded { get; set; }
        IDialogueStep NextStep { get; }
        Task<bool> ProcessStep(InteractivityService interactivity, Snowflake channel, IUser user);
    }
}
