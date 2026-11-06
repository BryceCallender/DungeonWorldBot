using DungeonWorldBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Remora.Rest.Core;

namespace DungeonWorldBot.Data;

/// <summary>
/// Notices what's changing on characters as it's saved, and adds a <see cref="CharacterEvent"/> for each thing worth
/// remembering: levels, stats, max HP, class, moves, debilities, bonds, goals, items, coins, shapes, and dying.
/// </summary>
/// <remarks>
/// Done on every save (see <see cref="CharacterEventsInterceptor"/>) rather than by each command, so nothing that changes
/// a character can forget to, from the bot or the dashboard. Only saves count: ExecuteUpdateAsync and ExecuteDeleteAsync
/// go straight to the database, so they mustn't be used on anything a timeline shows. A character being made gets one
/// event, not one for everything they start with. What it means to the table (which moves are only the bot keeping track of something, say) is left to
/// whoever shows the events.
/// </remarks>
internal static class CharacterEvents
{
    public static void Record(DungeonWorldContext context)
    {
        var tracker = context.ChangeTracker;
        if (tracker.AutoDetectChangesEnabled)
            tracker.DetectChanges();

        var at = DateTimeOffset.UtcNow;
        List<CharacterEvent> events = [];

        var made = tracker.Entries<Character>().Where(e => e.State == EntityState.Added).Select(e => e.Entity.ID).ToHashSet();
        foreach (var id in made)
            events.Add(new CharacterEvent { CharacterID = id, At = at, Kind = CharacterEventKind.Created });

        void Add(Snowflake? character, CharacterEventKind kind, string? subject = null, int? from = null, int? to = null)
        {
            if (character is { } id && !made.Contains(id))
                events.Add(new CharacterEvent { CharacterID = id, At = at, Kind = kind, Subject = subject, From = from, To = to });
        }

        foreach (var entry in tracker.Entries<Character>().Where(e => e.State == EntityState.Modified))
        {
            var character = entry.Entity.ID;
            if (Changed<int>(entry, nameof(Character.Level)) is { } level)
                Add(character, CharacterEventKind.LevelChanged, from: level.Was, to: level.Now);
            if (Changed<Status?>(entry, nameof(Character.Status)) is { } status)
            {
                var died = status.Now?.HasFlag(Status.Dead) == true;
                if (died != (status.Was?.HasFlag(Status.Dead) == true))
                    Add(character, died ? CharacterEventKind.Died : CharacterEventKind.Revived);
            }
        }

        foreach (var entry in tracker.Entries<Class>().Where(e => e.State == EntityState.Modified))
        {
            if (Changed<ClassType>(entry, nameof(Class.Type)) is { } type)
                Add(entry.Entity.CharacterID, CharacterEventKind.ClassChanged, type.Now.ToString(), (int)type.Was, (int)type.Now);
        }

        foreach (var entry in tracker.Entries<Stat>().Where(e => e.State == EntityState.Modified))
        {
            if (Changed<int>(entry, nameof(Stat.Value)) is { } value)
                Add(entry.Entity.CharacterID, CharacterEventKind.StatChanged, entry.Entity.StatType.ToString(), value.Was, value.Now);
        }

        foreach (var entry in tracker.Entries<Health>().Where(e => e.State == EntityState.Modified))
        {
            if (Changed<int>(entry, nameof(Health.MaxHP)) is { } max)
                Add(entry.Entity.CharacterID, CharacterEventKind.MaxHPChanged, from: max.Was, to: max.Now);
        }

        foreach (var entry in tracker.Entries<Ability>())
        {
            var (left, joined) = Owner(entry, AsCharacter);
            Add(joined, CharacterEventKind.MoveLearned, entry.Entity.Name, to: entry.Entity.LevelTaken);
            Add(left, CharacterEventKind.MoveForgotten, entry.Entity.Name);
        }

        foreach (var entry in tracker.Entries<Debility>())
        {
            var (left, joined) = Owner(entry, AsCharacter);
            Add(joined, CharacterEventKind.DebilityTaken, entry.Entity.StatType.ToString(), to: entry.Entity.Modifier);
            Add(left, CharacterEventKind.DebilityHealed, entry.Entity.StatType.ToString());
        }

        foreach (var entry in tracker.Entries<Bond>())
        {
            var (left, joined) = Owner(entry, AsCharacter);
            Add(joined, CharacterEventKind.BondMade, entry.Entity.TargetName);
            Add(left, CharacterEventKind.BondResolved, entry.Entity.TargetName);
        }

        foreach (var entry in tracker.Entries<Goal>())
        {
            var (_, joined) = Owner(entry, AsCharacter);
            Add(joined, CharacterEventKind.GoalSet, entry.Entity.Text);
            if (entry.State == EntityState.Modified && Changed<bool>(entry, nameof(Goal.Completed)) is { Was: false, Now: true })
                Add(AsCharacter(entry.Property("CharacterID").CurrentValue), CharacterEventKind.GoalCompleted, entry.Entity.Text);
        }

        foreach (var entry in tracker.Entries<Transformation>())
        {
            var (_, joined) = Owner(entry, AsCharacter);
            Add(joined, CharacterEventKind.ShapeLearned, entry.Entity.Name);
        }

        // Items hang off the inventory rather than the character. Keyed by what EF tracks, since new inventories all have
        // an ID of 0 until they're saved, while EF gives each one a temporary ID of its own that their items point at
        var inventories = tracker.Entries<Inventory>().ToDictionary(e => (int)e.Property(nameof(Inventory.ID)).CurrentValue!, e => e.Entity.CharacterID);
        Snowflake? OwnerOfInventory(object? id) => id is int key && inventories.TryGetValue(key, out var c) ? c : null;

        foreach (var entry in tracker.Entries<Item>())
        {
            var item = entry.Entity;
            var (left, joined) = Owner(entry, OwnerOfInventory, "InventoryID");

            if (item.Name.Equals("Coins", StringComparison.OrdinalIgnoreCase))
            {
                Add(joined, CharacterEventKind.CoinsChanged, from: 0, to: item.Amount);
                Add(left, CharacterEventKind.CoinsChanged, from: (int)entry.Property(nameof(Item.Amount)).OriginalValue!, to: 0);
                if (entry.State == EntityState.Modified && left is null && joined is null
                    && Changed<int>(entry, nameof(Item.Amount)) is { } coins)
                    Add(OwnerOfInventory(entry.Property("InventoryID").CurrentValue), CharacterEventKind.CoinsChanged, from: coins.Was, to: coins.Now);
                continue;
            }

            // How many arrows or rations are left changes all the time, so only gaining or losing the thing counts
            Add(joined, CharacterEventKind.ItemGained, item.Name, to: item.Amount);
            Add(left, CharacterEventKind.ItemLost, item.Name);
        }

        context.CharacterEvents.AddRange(events);
    }

    private record struct Change<T>(T Was, T Now);

    // The before and after of a property that changed, or null if it didn't
    private static Change<T>? Changed<T>(EntityEntry entry, string property)
    {
        var value = entry.Property(property);
        if (!value.IsModified)
            return null;

        var (was, now) = ((T)value.OriginalValue!, (T)value.CurrentValue!);
        return EqualityComparer<T>.Default.Equals(was, now) ? null : new Change<T>(was, now);
    }

    /// <returns>The character something was taken from, and the one it was given to, if either.</returns>
    private record Ownership(Snowflake? Left, Snowflake? Joined);

    private static Ownership Owner(EntityEntry entry, Func<object?, Snowflake?> toCharacter,
        string foreignKey = "CharacterID")
    {
        var key = entry.Property(foreignKey);
        return entry.State switch
        {
            EntityState.Added => new(null, toCharacter(key.CurrentValue)),
            EntityState.Deleted => new(toCharacter(key.OriginalValue), null),
            // Taken out of a list with an optional key, which leaves it behind with none
            EntityState.Modified when key.IsModified && toCharacter(key.OriginalValue) is var was
                                      && toCharacter(key.CurrentValue) is var now && was != now => new(was, now),
            _ => new(null, null),
        };
    }

    private static Snowflake? AsCharacter(object? key) => key as Snowflake?;
}
