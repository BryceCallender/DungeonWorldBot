using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;
using Remora.Discord.API.Abstractions.Objects;
using System.Drawing;
using DungeonWorldBot.Services.Interactivity;

namespace DungeonWorldBot.Services.Implementation.Steps
{
    public class TextStep : DialogueStepBase
    {
        private IDialogueStep _nextStep;
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
        public void setNextStep(IDialogueStep nextStep, bool edited = false)
        {
            _nextStep = nextStep;
            _edited = edited;
        }

        public override async Task<bool> ProcessStep(InteractivityService interactivity, Snowflake channel, Snowflake userId)
        {

            var embedFields = new List<IEmbedField>
            {
                new EmbedField(Name: "To Cancel the Character Creation", Value: "Type the /cancel command")
            };

            if (_minLength.HasValue)
            {
                embedFields.Add(new EmbedField("Min Length:", $"{_minLength.Value} characters"));
            }
            if (_maxLength.HasValue)
            {
                embedFields.Add(new EmbedField("Max Length:", $"{_maxLength.Value} characters"));
            }

            var embed = new Embed
            {
                Title = "Character Creation",
                Description = $"{_content}",
                Colour = Color.Yellow,
                Fields = embedFields,
            };

            while (true)
            {
                var message = await _feedbackService.SendPrivateEmbedAsync(userId, embed);
                if (!message.IsSuccess)
                    return true;

                var messageResult = await interactivity.GetNextMessageAsync(channel, message.Entity.ID, TimeSpan.FromMinutes(1));

                if (!messageResult.IsSuccess)
                    return true;

                if (messageResult.Entity.Content.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (_minLength.HasValue)
                {
                    if (messageResult.Entity.Content.Length < _minLength.Value)
                    {
                        await TryAgain(channel, $"Your input is {_minLength.Value - messageResult.Entity.Content.Length} characters too short");
                        continue;
                    }
                }

                if (_maxLength.HasValue)
                {
                    if (messageResult.Entity.Content.Length > _maxLength.Value)
                    {
                        await TryAgain(channel, $"Your input is {messageResult.Entity.Content.Length - _maxLength.Value} characters too long");
                        continue;
                    }
                }

                OnValidResult(messageResult.Entity.Content);

                return false;
            }

        }
    }
}
