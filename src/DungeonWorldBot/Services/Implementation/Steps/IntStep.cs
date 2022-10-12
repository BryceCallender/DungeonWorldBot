using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;
using Remora.Discord.API.Abstractions.Objects;
using System.Drawing;
using DungeonWorldBot.Services.Interactivity;

namespace DungeonWorldBot.Services.Implementation.Steps
{
    public class IntStep : DialogueStepBase
    {
        private IDialogueStep _nextStep;
        private readonly int? _minValue;
        private readonly int? _maxValue;

        public IntStep( string content, FeedbackService feedbackService, IDialogueStep nextStep, int? minValue = null, int? maxValue = null) : base (content, feedbackService)
        {
            _nextStep = nextStep;
            _minValue = minValue;
            _maxValue = maxValue;
        }

        public Action<int> OnValidResult { get; set; } = delegate { };

        public override IDialogueStep NextStep => _nextStep;
        public void setNextStep(IDialogueStep nextStep)
        {
            _nextStep = nextStep;
        }

        public override async Task<bool> ProcessStep(InteractivityService interactivity, Snowflake channel, IUser user)
        {
            var embedFields = new List<IEmbedField>
            {
                new EmbedField(Name: "To Cancel the Character Creation", Value: "Type the /cancel command")
            };

            if (_minValue.HasValue)
            {
                embedFields.Add(new EmbedField("Min Value:", $"{_minValue.Value}"));
            }
            if (_maxValue.HasValue)
            {
                embedFields.Add(new EmbedField("Max Value:", $"{_maxValue.Value}"));
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
                var message = await _feedbackService.SendPrivateEmbedAsync(user.ID, embed);
                if (!message.IsSuccess)
                    return true;

                var messageResult = await interactivity.GetNextMessageAsync(channel, message.Entity.ID, TimeSpan.FromMinutes(1));

                if (!messageResult.IsSuccess)
                    return true;

                if (messageResult.Entity.Content.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if(!int.TryParse(messageResult.Entity.Content, out int inputValue))
                {
                    await TryAgain(channel, $"Your input is not a whole number.");
                    continue;
                }

                if (_minValue.HasValue)
                {
                    if (inputValue < _minValue.Value)
                    {
                        await TryAgain(channel, $"Your input value: {inputValue} is smaller than: {_minValue.Value}");
                        continue;
                    }
                }

                if (_maxValue.HasValue)
                {
                    if (inputValue > _maxValue.Value)
                    {
                        await TryAgain(channel, $"Your input value: {inputValue} is larger than: {_maxValue.Value}");
                        continue;
                    }
                }

                OnValidResult(inputValue);

                return false;
            }

        }
    }
}
