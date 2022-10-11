using Remora.Discord.API.Objects;
using Remora.Discord.Gateway;
using Remora.Discord.Commands.Feedback.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Remora.Discord.Commands.Feedback.Services;
using DSharpPlus.Entities;
using DSharpPlus;
using Remora.Rest.Core;
using Remora.Discord.API.Abstractions.Objects;
using DungeonWorldBot.Services.Interactivity;

namespace DungeonWorldBot.Services.Implementation.Steps
{
    public abstract class DialogueStepBase : IDialogueStep
    {
        protected readonly string _content;
        protected readonly FeedbackService _feedbackService;

        public DialogueStepBase(string content, FeedbackService feedbackService)
        {
            _content = content;
            _feedbackService = feedbackService; 
        }

        public Action<DiscordMessage> OnMessageAdded { get; set; } = delegate { };

        public abstract IDialogueStep NextStep { get; }

        public abstract Task<bool> ProcessStep(InteractivityService interactivity, Snowflake channel, IUser user);

        protected async Task TryAgain(Snowflake channel, string problem)
        {
            var embed = await _feedbackService.SendMessageAsync(channel, new FeedbackMessage($"There was a problem with your input: {problem}", Color.Red));
        }
    }
}
