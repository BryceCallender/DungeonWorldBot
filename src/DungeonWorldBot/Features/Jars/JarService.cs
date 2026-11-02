using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Jars;

[Scoped<IJarService>]
public class JarService(DungeonWorldContext dungeonWorldContext) : IJarService
{
    public async Task StoreAsync(Character character, string corpse)
    {
        var jars = JarRules.Jars(character) ?? AddJars(character);
        var slots = JarRules.Slots(character).ToList();
        slots[slots.FindIndex(JarRules.IsEmpty)] = corpse.Trim();

        // A new list, so the change to the saved contents is noticed
        jars.Contents = slots;
        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task EmptyAsync(Character character, string corpse)
    {
        if (JarRules.Jars(character) is not { } jars)
            return;

        var slots = JarRules.Slots(character).ToList();
        var index = slots.FindIndex(s => s.Equals(corpse.Trim(), StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return;

        slots[index] = JarRules.Empty;
        jars.Contents = slots;
        await dungeonWorldContext.SaveChangesAsync();
    }

    private static HexedJars AddJars(Character character)
    {
        var jars = new HexedJars { Name = JarRules.JarsName, Amount = JarRules.BaseJars, Weight = 1 };
        character.Inventory.Items ??= [];
        character.Inventory.Items.Add(jars);
        return jars;
    }
}
