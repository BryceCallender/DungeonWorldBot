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
using DungeonWorldBot.Services.Implementation;

namespace DungeonWorldBot.Commands;

[Group("inventory")]
public class InventoryCommand : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly IInventoryService _inventoryService;

    public InventoryCommand( 
        ICommandContext commandContext, 
        FeedbackService feedbackService,
        ICharacterService characterService, 
        IInventoryService inventoryService)
    {
        _context = commandContext;
        _feedbackService = feedbackService;
        _characterService = characterService;
        _inventoryService = inventoryService;
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

        if (amount < 0)
            return await ReplyWithErrorAsync($"You cannot add a negative amount of an item to your inventory");


        if (character.Inventory.Items.Exists(item => item.Name == itemName))
            character.Inventory.Items.Find(item => item.Name == itemName).Amount += amount;
        else
            character.Inventory.Items.Add(new Item { Name = itemName, Amount = amount });

        await _inventoryService.SaveInventoryAsync();


        return await _feedbackService.SendContextualMessageAsync(new FeedbackMessage($"You have successfully added {amount} {itemName} to your inventory", Color.Green));
    }

    [Command("remove")]
    [Description("Removes an item or specified amount from your inventory (Careful of the spelling)")]
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
            }      

        }
        else
            return await ReplyWithErrorAsync($"You do not have {itemName} in your inventory");

        await _inventoryService.SaveInventoryAsync();

        if (amount < 0)
            return await _feedbackService.SendContextualMessageAsync(new FeedbackMessage($"You have successfully removed {itemName} from your inventory", Color.Green));
        else
            return await _feedbackService.SendContextualMessageAsync(new FeedbackMessage($"You have successfully removed {amount} {itemName} from your inventory", Color.Green));
    }

    [Command("receiveCoins")]
    [Description("Adds more coins to your inventory")]
    public async Task<IResult> ReceiveInventoryAsync(int amount)
    {
        var character = await _characterService.GetCharacterFromUserAsync(_context.User);

        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to add to its inventory. Try using /character create");
        if (character.Inventory.Items == null)
            return await ReplyWithErrorAsync("Your Inventory Doesn't Exist");

        if(amount < 0)
            return await ReplyWithErrorAsync($"You cannot add a negative amount of coins to your inventory");

        if (character.Inventory.Items.Exists(item => item.Name == "Coins"))
            character.Inventory.Items.Find(item => item.Name == "Coins").Amount += amount;
        else
            character.Inventory.Items.Add(new Item { Name = "Coins", Amount = amount });

        await _inventoryService.SaveInventoryAsync();


        return await _feedbackService.SendContextualMessageAsync(new FeedbackMessage($"You have successfully added {amount} coins to your inventory", Color.Green));
    }

    [Command("payCoins")]
    [Description("Uses the specified amount of coins and takes them out of your inventory")]
    public async Task<IResult> PayFromInventoryAsync(int amount)
    {
        var character = await _characterService.GetCharacterFromUserAsync(_context.User);

        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to add to its inventory. Try using /character create");
        if (character.Inventory.Items == null)
            return await ReplyWithErrorAsync("Your Inventory Doesn't Exist");


        if (character.Inventory.Items.Exists(item => item.Name == "Coins"))
        {
            var foundItem = character.Inventory.Items.Find(item => item.Name == "Coins");

            if (amount < 0)
                return await ReplyWithErrorAsync($"You cannot remove a negative amount of coins from your inventory");
            else
            {
                foundItem.Amount -= amount;
                if (foundItem.Amount < 0)
                    character.Inventory.Items.RemoveAll(item => item.Name == foundItem.Name);
            }

        }
        else
            return await ReplyWithErrorAsync($"You do not have any coins in your inventory");

        await _inventoryService.SaveInventoryAsync();


        return await _feedbackService.SendContextualMessageAsync(new FeedbackMessage($"You have successfully removed {amount} coins to your inventory", Color.Green));
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