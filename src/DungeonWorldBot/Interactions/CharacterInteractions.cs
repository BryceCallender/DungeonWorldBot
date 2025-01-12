// using System.Drawing;
// using DungeonWorldBot.Commands;
// using DungeonWorldBot.Data.Entities;
// using DungeonWorldBot.Services;
// using DungeonWorldBot.Services.Interactivity;
// using EnumsNET;
// using Microsoft.Extensions.Logging;
// using Remora.Commands.Attributes;
// using Remora.Discord.API.Abstractions.Objects;
// using Remora.Discord.API.Abstractions.Rest;
// using Remora.Discord.API.Objects;
// using Remora.Discord.Commands.Contexts;
// using Remora.Discord.Commands.Feedback.Messages;
// using Remora.Discord.Commands.Feedback.Services;
// using Remora.Discord.Interactivity;
// using Remora.Results;
//
// namespace DungeonWorldBot.Interactions;
//
// public class CharacterInteractions : InteractionGroup
// {
//     private readonly InteractionContext _context;
//     private readonly IDiscordRestChannelAPI _channelAPI;
//     private readonly FeedbackService _feedback;
//     private readonly ILogger<CharacterInteractions> _logger;
//     private readonly InteractivityService _interactivityService;
//     private readonly ICharacterService _characterService;
//     
//     public CharacterInteractions
//     (
//         InteractionContext context,
//         IDiscordRestChannelAPI channelAPI,
//         FeedbackService feedback,
//         ILogger<CharacterInteractions> logger,
//         InteractivityService interactivityService,
//         ICharacterService characterService)
//     {
//         _context = context;
//         _channelAPI = channelAPI;
//         _feedback = feedback;
//         _logger = logger;
//         _interactivityService = interactivityService;
//         _characterService = characterService;
//     }
//     
//     [SelectMenu("alignment-dropdown")]
//     public async Task<Result> SetClassAsync(IReadOnlyList<string> values)
//     {
//         if (!_context.Message.IsDefined(out var message))
//         {
//             return new InvalidOperationError("Interaction without a message?");
//         }
//
//         if (values.Count != 1)
//         {
//             return new InvalidOperationError("Only one element may be selected at any one time.");
//         }
//
//         var alignmentValue = values.Single();
//         
//         var embed = new Embed
//         (
//             Colour: _feedback.Theme.Primary,
//             Description: $"You picked {alignmentValue}.",
//             Footer: new EmbedFooter("Not the right alignment? Choose again")
//         );
//
//         var components = new List<IMessageComponent>(message.Components.Value);
//         var dropdown = (ISelectMenuComponent)((IActionRowComponent)components[0]).Components[0];
//         
//         var selected = dropdown.Options.Single(o => o.Value == alignmentValue);
//         var newComponents = new List<IMessageComponent>
//         {
//             new ActionRowComponent(new[]
//             {
//                 new SelectMenuComponent
//                 (
//                     dropdown.CustomID,
//                     dropdown.Options,
//                     selected.Label,
//                     dropdown.MinValues,
//                     dropdown.MaxValues,
//                     dropdown.IsDisabled
//                 )
//             })
//         };
//
//         var character = await _characterService.GetCharacterFromUserAsync(_context.User);
//         
//         if (character is null)
//         {
//             return Result.FromError<string>("Character is not created.");
//         }
//         
//         var result = Enum.TryParse<Alignment>(alignmentValue, out var alignment);
//         await _characterService.ChangeCharacterAlignment(character, result ? alignment : Alignment.Unknown);
//         
//         return (Result)await _channelAPI.EditMessageAsync
//         (
//             _context.ChannelID,
//             message.ID,
//             embeds: new[] { embed },
//             components: newComponents,
//             ct: this.CancellationToken
//         );
//     }
// }