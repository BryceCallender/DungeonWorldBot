using Remora.Discord.Commands.Feedback.Messages;
using System.Drawing;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;
using Remora.Discord.API.Abstractions.Objects;
using DungeonWorldBot.Services.Interactivity;
using Remora.Discord.API.Objects;

namespace DungeonWorldBot.Services.Implementation.Steps
{
    public abstract class DialogueStepBase : IDialogueStep
    {
        protected readonly string _content;
        protected readonly FeedbackService _feedbackService;
        public bool _edited { get; set;}

        public DialogueStepBase(string content, FeedbackService feedbackService)
        {
            _content = content;
            _feedbackService = feedbackService;
            _edited = false;
        }

        public Action<Message> OnMessageAdded { get; set; } = delegate { };

        public abstract IDialogueStep NextStep { get; }

        public abstract Task<bool> ProcessStep(InteractivityService interactivity, Snowflake channel, IUser user);

        protected async Task TryAgain(Snowflake channel, string problem)
        {
            var embed = await _feedbackService.SendMessageAsync(channel, new FeedbackMessage($"There was a problem with your input: {problem}", Color.Red));
        }
    }
}
