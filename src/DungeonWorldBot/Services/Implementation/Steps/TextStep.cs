using DSharpPlus;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Gateway;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using System.Security.Cryptography.X509Certificates;
using DSharpPlus.Interactivity.Extensions;
using Remora.Rest.Core;
using Remora.Discord.API.Abstractions.Objects;
using System.Drawing;
using DungeonWorldBot.Services.Interactivity;

namespace DungeonWorldBot.Services.Implementation.Steps
{
    public class TextStep : DialogueStepBase
    {
        private readonly IDialogueStep _nextStep;
        private readonly int? _minLength;
        private readonly int? _maxLength;

        public TextStep( string content, FeedbackService feedbackService, IDialogueStep nextStep, int? minlength = null, int? maxlength = null) : base (content, feedbackService)
        {
            _nextStep = nextStep;
            _minLength = minlength;
            _maxLength = maxlength;
        }

        public Action<string> OnValidResult { get; set; } = delegate { };

        public override IDialogueStep NextStep => _nextStep;

        public override async Task<bool> ProcessStep(InteractivityService interactivity, Snowflake channel, IUser user)
        {
            var feedbackMessage = new FeedbackMessage(_content, Color.Yellow);
            
            await _feedbackService.SendPrivateMessageAsync(user.ID, feedbackMessage);
            
            var messageResult = await interactivity.GetNextMessageAsync(channel, TimeSpan.FromMinutes(1));

            if (!messageResult.IsSuccess) 
                return true;
            
            if (messageResult.Entity.Content.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (_minLength.HasValue)
            {
                await TryAgain(channel, $"Your input is {_minLength.Value - messageResult.Entity.Content.Length} characters too short");
            }

            if (_maxLength.HasValue)
            {
                await TryAgain(channel, $"Your input is {messageResult.Entity.Content.Length - _maxLength.Value} characters too long");
            }

            OnValidResult(messageResult.Entity.Content);

            return false;

        }
    }
}
