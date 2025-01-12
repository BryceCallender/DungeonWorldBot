using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;
using Remora.Discord.API.Abstractions.Objects;
using System.Drawing;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Services.Interactivity;

namespace DungeonWorldBot.Services.Implementation.Steps
{
    public class EndingStep : DialogueStepBase
    {
        private IDialogueStep _nextStep;
        private readonly int? _minValue;
        private readonly int? _maxValue;
        private Character _character;

        public EndingStep( string content, FeedbackService feedbackService, IDialogueStep nextStep, Character character, int? minValue = null, int? maxValue = null) : base (content, feedbackService)
        {
            _nextStep = nextStep;
            _minValue = minValue;
            _maxValue = maxValue;
            _character = character;
        }

        public Action<int> OnValidResult { get; set; } = delegate { };

        public override IDialogueStep NextStep => _nextStep;

        public void setNextStep(IDialogueStep nextStep, bool edited = false)
        {
            _nextStep = nextStep;
            _edited = edited;
        }

        public override async Task<bool> ProcessStep(InteractivityService interactivity, Snowflake channel, Snowflake userId)
        {
            var stats = string.Empty;
            var embedFields = new List<IEmbedField>();


            embedFields.Add(new EmbedField(Name: "Name", Value: _character.Name, IsInline: false));
            embedFields.Add(new EmbedField(Name: "Race", Value: _character.Race.ToString(), IsInline: false));
            embedFields.Add(new EmbedField(Name: "Class", Value: $"{_character.Class.Type} Level: 1", IsInline: false));
            embedFields.AddRange(_character.Stats.Select(s => new EmbedField(Name: s.StatType.ToString(), Value: s.Value.ToString(), true)).ToArray());

            //embedFields.Add(new EmbedField(Name: "Health", Value: character.Health.ToDisplay(), IsInline: false));
            //embedFields.Add(new EmbedField(Name: "Status", Value: character.Status?.ToString() ?? "Unknown..."));
            embedFields.Add(new EmbedField(Name: "Alignment", Value: _character.Alignment.ToString()));
            //embedFields.Add(new EmbedField(Name: "Debilities", Value: "None"));
            embedFields.Add(new EmbedField(Name: "Total Armor", Value: _character.ArmorRating.ToString()));

            embedFields.Add(new EmbedField(Name: "Inventory", Value: "--------------"));
            embedFields.AddRange(_character.Inventory.Items.Select(s => new EmbedField(Name: s.Name, Value: s.Amount.ToString(), true)).ToArray());

            embedFields.Add(new EmbedField(Name: "Would you like to edit any of this information?", Value: _content));


            embedFields.Add(new EmbedField(Name: "To Cancel the Character Creation", Value: "Type the /cancel command"));

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
                Type = EmbedType.Rich,
                Fields = embedFields,
                Colour = _feedbackService.Theme.Primary
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
