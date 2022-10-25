using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Text;
using DungeonWorldBot.API.Models;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Services;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using Remora.Discord.Commands.Feedback.Messages;
using System.Drawing;

namespace DungeonWorldBot.Commands;

[Group("inventory")]
public class InventoryCommand : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly IRollService _rollService;
    private readonly ICharacterService _characterService;

    public InventoryCommand( 
        ICommandContext commandContext, 
        FeedbackService feedbackService,
        IRollService rollService,
        ICharacterService characterService)
    {
        _context = commandContext;
        _feedbackService = feedbackService;
        _rollService = rollService;
        _characterService = characterService;
    }
    
    [Command("store")]
    [Description("Adds an item to your inventory (Careful of the spelling)")]
    public async Task<IResult> StoreInInventoryAsync(string itemName, int amount)
    {
        var character = await _characterService.GetCharacterFromUserAsync(_context.User);

        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to add to its inventory. Try using /character create");
        if (character.Inventory.Items == null)
            return await ReplyWithErrorAsync("Your Inventory Doesn't Exist");


        if (character.Inventory.Items.Exists(item => item.Name == itemName))
            character.Inventory.Items.Find(item => item.Name == itemName).Amount += amount;
        else
            character.Inventory.Items.Add(new Item { Name = itemName, Amount = amount });


        return await _feedbackService.SendContextualMessageAsync(new FeedbackMessage($"You have successfully added {amount} {itemName} to your inventory", Color.Green));
    }

    [Command("remove")]
    [Description("Removes an item from your inventory (Careful of the spelling)")]
    public async Task<IResult> RemoveInInventoryAsync(string itemName, int amount = -1)
    {
        var character = await _characterService.GetCharacterFromUserAsync(_context.User);

        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to remove from its inventory. Try using /character create");
        if (character.Inventory.Items == null)
            return await ReplyWithErrorAsync("Your Inventory Doesn't Exist");
        if (character.Inventory.Items.Count == 0)
            return await ReplyWithErrorAsync("You don't have any items to remove");


        if (character.Inventory.Items.Exists(item => item.Name == itemName))
        {
            var foundItem = character.Inventory.Items.Find(item => item.Name == itemName);

            if (amount < 0)
                character.Inventory.Items.Remove(foundItem);
            else
            {
                foundItem.Amount -= amount;
                if(foundItem.Amount < 0)
                    character.Inventory.Items.RemoveAll(item => item.Name == foundItem.Name);
                else
                    character.Inventory.Items.Find(item => item.Name == foundItem.Name).Amount -= amount;
            }      

        }
        else
            return await ReplyWithErrorAsync($"You do not have {itemName} in your inventory");

        if(amount < 0)
            return await _feedbackService.SendContextualMessageAsync(new FeedbackMessage($"You have successfully removed {itemName} from your inventory", Color.Green));
        else
            return await _feedbackService.SendContextualMessageAsync(new FeedbackMessage($"You have successfully removed {amount} {itemName} from your inventory", Color.Green));
    }


    private async Task<Result> ReplyWithFailureAsync()
    {
        return (Result)await _feedbackService.SendContextualErrorAsync
        (
            "Failed to interact with the Inventory.",
            ct: CancellationToken
        );
    }
    
    private async Task<Result> ReplyWithErrorAsync(string error)
    {
        return (Result)await _feedbackService.SendContextualErrorAsync
        (
            error,
            ct: CancellationToken
        );
    }

}