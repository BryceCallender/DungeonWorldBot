﻿using System;
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
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;

namespace DungeonWorldBot.Interactions
{
    public class DialogueHandler
    {
        private readonly InteractivityService _interactivity;
        private readonly IUser _user;
        private IDialogueStep _currentStep;
        private readonly IDiscordRestUserAPI _userAPI;

        private readonly List<DiscordMessage> _messages = new List<DiscordMessage>();

        public Snowflake ChannelID { get; set; }
        
        public DialogueHandler(
            InteractivityService interactivity,
            IUser user, 
            IDialogueStep startingStep,
            IDiscordRestUserAPI userAPI)
        {
            _interactivity = interactivity;
            _user = user;   
            _currentStep = startingStep;
            _userAPI = userAPI;
        }

        public async Task<bool> ProcessDialogue(FeedbackService feedbackService)
        {
            while (_currentStep != null)
            {
                _currentStep.OnMessageAdded += (message) => _messages.Add(message);
                
                var getUserDM = await _userAPI.CreateDMAsync(_user.ID, CancellationToken.None);
                if (!getUserDM.IsSuccess)
                {
                    return true;
                }

                var dm = getUserDM.Entity;
                ChannelID = dm.ID;
                
                var canceled = await _currentStep.ProcessStep(_interactivity, ChannelID, _user).ConfigureAwait(false);

                if (canceled)
                {
                    await DeleteMessages().ConfigureAwait(false);

                    await feedbackService.SendMessageAsync(ChannelID, new FeedbackMessage("You Have Successfully Canceled your Character Creation", Color.Green));

                    return false;
                }

                _currentStep = _currentStep.NextStep;

            }
            return true;
        }

        public async Task DeleteMessages()
        {
            if (ChannelID == _user.ID) { return; }

            foreach (var message in _messages)
            {
                await message.DeleteAsync();
            }
        }
    }
}
