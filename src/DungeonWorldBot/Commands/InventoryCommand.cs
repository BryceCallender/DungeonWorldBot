using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Services;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using Remora.Discord.Commands.Feedback.Messages;
using System.Drawing;
using DungeonWorldBot.Services.Implementation;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Pagination.Extensions;

namespace DungeonWorldBot.Commands;

[Group("inventory")]
public class InventoryCommand : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly IInventoryService _inventoryService;

    private const int PAGE_SIZE = 20;

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

    [Command("show")]
    [Description("Shows a paginated list of all your inventory items")]
    public async Task<IResult> DisplayInventory()
    {
        if (!_context.TryGetUserID(out var userId))
        {
            throw new NotSupportedException();
        }
        
        var character = await _characterService.GetCharacterFromUserAsync(userId);

        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to view its inventory. Try using /character create");

        var items = character.Inventory?.Items ?? new List<Item>();
        var coinAmount = items
            .Where(i => i.Name.Equals("Coins", StringComparison.OrdinalIgnoreCase))
            .Sum(i => i.Amount);
        var otherItems = items
            .Where(i => !i.Name.Equals("Coins", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var inventoryEmbeds = otherItems
            .Chunk(PAGE_SIZE)
            .Select(page => new Embed(
                Title: "Inventory",
                Description: $"Coins (🪙): {coinAmount}",
                Colour: _feedbackService.Theme.Primary,
                Fields: page.Select(item => (IEmbedField)new EmbedField(Name: item.Name.ToLower(), Value: item.Amount.ToString(), IsInline: true)).ToList()))
            .ToList();

        if (inventoryEmbeds.Count == 0)
        {
            return await _feedbackService.SendContextualEmbedAsync(
                new Embed(
                    Title: "Inventory",
                    Description: $"Coins (🪙): {coinAmount}\n\nYour inventory is empty.",
                    Colour: _feedbackService.Theme.Primary),
                ct: CancellationToken);
        }

        return await _feedbackService.SendContextualPaginatedMessageAsync(
            userId,
            inventoryEmbeds
        );
    }
    
    [Command("store")]
    [Description("Adds an item to your inventory (Careful of the spelling)")]
    public async Task<IResult> StoreInInventoryAsync(string itemName, int amount)
    {
        if (!_context.TryGetUserID(out var userId))
        {
            throw new NotSupportedException();
        }
        
        var character = await _characterService.GetCharacterFromUserAsync(userId);

        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to add to its inventory. Try using /character create");
        if (character.Inventory.Items == null)
            return await ReplyWithErrorAsync("Your Inventory Doesn't Exist");

        if (amount < 0)
            return await ReplyWithErrorAsync($"You cannot add a negative amount of an item to your inventory");


        if (character.Inventory.Items.Exists(item => item.Name.Equals(itemName, StringComparison.CurrentCultureIgnoreCase)))
            character.Inventory.Items.Single(item => item.Name.Equals(itemName, StringComparison.CurrentCultureIgnoreCase)).Amount += amount;
        else
            character.Inventory.Items.Add(new Item { Name = itemName.ToLower(), Amount = amount });

        await _inventoryService.SaveInventoryAsync();


        return await _feedbackService.SendContextualMessageAsync(new FeedbackMessage($"You have successfully added {amount} {itemName.ToLower()} to your inventory", Color.Green));
    }

    [Command("remove")]
    [Description("Removes an item or specified amount from your inventory (Careful of the spelling)")]
    public async Task<IResult> RemoveInInventoryAsync(string itemName, int amount = -1)
    {
        if (!_context.TryGetUserID(out var userId))
        {
            throw new NotSupportedException();
        }
        
        var character = await _characterService.GetCharacterFromUserAsync(userId);

        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to remove from its inventory. Try using /character create");
        if (character.Inventory.Items == null)
            return await ReplyWithErrorAsync("Your Inventory Doesn't Exist");
        if (character.Inventory.Items.Count == 0)
            return await ReplyWithErrorAsync("You don't have any items to remove");


        if (character.Inventory.Items.Exists(item => item.Name.Equals(itemName, StringComparison.CurrentCultureIgnoreCase)))
        {
            var foundItem = character.Inventory.Items.Single(item => item.Name.Equals(itemName, StringComparison.CurrentCultureIgnoreCase));

            if (amount < 0)
            {
                character.Inventory.Items.Remove(foundItem);
            }
            else
            {
                foundItem.Amount -= amount;
                if (foundItem.Amount <= 0)
                    character.Inventory.Items.Remove(foundItem);
            }      

        }
        else
        {
            return await ReplyWithErrorAsync($"You do not have {itemName} in your inventory");
        }

        await _inventoryService.SaveInventoryAsync();

        if (amount < 0)
            return await _feedbackService.SendContextualMessageAsync(new FeedbackMessage($"You have successfully removed {itemName.ToLower()} from your inventory", Color.Green));
        else
            return await _feedbackService.SendContextualMessageAsync(new FeedbackMessage($"You have successfully removed {amount} {itemName.ToLower()} from your inventory", Color.Green));
    }

    [Command("receive-coins")]
    [Description("Adds more coins to your inventory")]
    public async Task<IResult> ReceiveInventoryAsync(int amount)
    {
        if (!_context.TryGetUserID(out var userId))
        {
            throw new NotSupportedException();
        }
        
        var character = await _characterService.GetCharacterFromUserAsync(userId);

        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to add to its inventory. Try using /character create");
        if (character.Inventory.Items == null)
            return await ReplyWithErrorAsync("Your Inventory Doesn't Exist");

        if(amount < 0)
            return await ReplyWithErrorAsync($"You cannot add a negative amount of coins to your inventory");

        if (character.Inventory.Items.Exists(item => item.Name.Equals("Coins", StringComparison.CurrentCultureIgnoreCase)))
            character.Inventory.Items.Single(item => item.Name.Equals("Coins", StringComparison.CurrentCultureIgnoreCase)).Amount += amount;
        else
            character.Inventory.Items.Add(new Item { Name = "Coins", Amount = amount });

        await _inventoryService.SaveInventoryAsync();


        return await _feedbackService.SendContextualMessageAsync(new FeedbackMessage($"You have successfully added {amount} coins to your inventory", Color.Green));
    }

    [Command("pay-coins")]
    [Description("Uses the specified amount of coins and takes them out of your inventory")]
    public async Task<IResult> PayFromInventoryAsync(int amount)
    {
        if (!_context.TryGetUserID(out var userId))
        {
            throw new NotSupportedException();
        }
        
        var character = await _characterService.GetCharacterFromUserAsync(userId);

        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to add to its inventory. Try using /character create");
        if (character.Inventory.Items == null)
            return await ReplyWithErrorAsync("Your Inventory Doesn't Exist");


        if (character.Inventory.Items.Exists(item => item.Name.Equals("Coins", StringComparison.CurrentCultureIgnoreCase)))
        {
            var foundItem = character.Inventory.Items.Single(item => item.Name.Equals("Coins", StringComparison.CurrentCultureIgnoreCase));

            if (amount < 0)
                return await ReplyWithErrorAsync($"You cannot remove a negative amount of coins from your inventory");
            if (amount > foundItem.Amount)
                return await ReplyWithErrorAsync($"You only have {foundItem.Amount} coins");

            foundItem.Amount -= amount;
            if (foundItem.Amount == 0)
                character.Inventory.Items.Remove(foundItem);

        }
        else
            return await ReplyWithErrorAsync($"You do not have any coins in your inventory");

        await _inventoryService.SaveInventoryAsync();


        return await _feedbackService.SendContextualMessageAsync(new FeedbackMessage($"You have successfully paid {amount} coins from your inventory", Color.Green));
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