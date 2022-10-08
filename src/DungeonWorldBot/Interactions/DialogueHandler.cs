using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DungeonWorldBot.Services;
using DungeonWorldBot.Services.Interactivity;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;

namespace DungeonWorldBot.Interactions
{
    public class DialogueHandler
    {
        private readonly InteractivityService _interactivity;
        private readonly Snowflake _channel;
        private readonly IUser _user;
        private IDialogueStep _currentStep;

        private readonly List<DiscordMessage> messages = new List<DiscordMessage>();
        public DialogueHandler(InteractivityService interactivity, Snowflake channel, IUser user, IDialogueStep startingStep)
        {
            _interactivity = interactivity;   
            _channel = channel; 
            _user = user;   
            _currentStep = startingStep;    
        }

        public async Task<bool> ProcessDialogue(FeedbackService feedbackService)
        {
            while (_currentStep != null)
            {
                _currentStep.OnMessageAdded += (message) => messages.Add(message);
                bool canceled = await _currentStep.ProcessStep(_interactivity, _channel, _user).ConfigureAwait(false);

                if (canceled)
                {
                    await DeleteMessages().ConfigureAwait(false);


                    await feedbackService.SendMessageAsync(_channel, new FeedbackMessage("You Have Successfully Canceled your Character Creation", Color.Green));

                    return false;
                }

                _currentStep = _currentStep.NextStep;

            }
            return true;
        }

        public async Task DeleteMessages()
        {
            if (_channel == _user.ID) { return; }

            foreach (var message in messages)
            {
                await message.DeleteAsync();
            }
        }
    }
}
