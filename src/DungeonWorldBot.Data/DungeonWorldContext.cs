using DungeonWorldBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Data;

public class DungeonWorldContext(DbContextOptions<DungeonWorldContext> options) : DbContext(options)
{
    public DbSet<Character> Characters => Set<Character>();

    public DbSet<RollRecord> Rolls => Set<RollRecord>();

    public DbSet<Npc> Npcs => Set<Npc>();

    public DbSet<Encounter> Encounters => Set<Encounter>();

    public DbSet<Enemy> Enemies => Set<Enemy>();

    public DbSet<EncounterAction> EncounterActions => Set<EncounterAction>();

    public DbSet<EncounterParticipant> EncounterParticipants => Set<EncounterParticipant>();

    public DbSet<MoveRequest> MoveRequests => Set<MoveRequest>();

    public DbSet<MonsterTemplate> MonsterTemplates => Set<MonsterTemplate>();

    public DbSet<GameSession> Sessions => Set<GameSession>();

    public DbSet<CharacterEvent> CharacterEvents => Set<CharacterEvent>();

    // Added here rather than where the context is registered, so every context notes what changed on characters for
    // their timelines however it's made, from the bot, the dashboard or a test
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(CharacterEventsInterceptor.Instance);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Npc>(npc =>
        {
            npc.HasIndex(n => n.Name).IsUnique();
            // Names are looked up however they're typed
            npc.Property(n => n.Name).UseCollation("NOCASE");
        });

        // The bot and the dashboard can both hurt or heal these at once. A save only lands if the HP is still what it
        // read, so one side can't quietly undo the other's hit; the loser gets a DbUpdateConcurrencyException.
        modelBuilder.Entity<Enemy>().Property(e => e.CurrentHP).IsConcurrencyToken();
        modelBuilder.Entity<Npc>().Property(n => n.CurrentHP).IsConcurrencyToken();
        modelBuilder.Entity<Health>().Property(h => h.CurrentHP).IsConcurrencyToken();

        modelBuilder.Entity<Encounter>(encounter =>
        {
            // A scene has one fight going on at a time, even when the bot and the dashboard start one at once
            encounter.HasIndex(e => e.SceneID).IsUnique().HasFilter($"\"{nameof(Encounter.EndedAt)}\" IS NULL");
            encounter.HasMany(e => e.Enemies)
                .WithOne(e => e.Encounter)
                .HasForeignKey(e => e.EncounterID)
                .OnDelete(DeleteBehavior.Cascade);
            encounter.HasMany(e => e.Actions)
                .WithOne(a => a.Encounter)
                .HasForeignKey(a => a.EncounterID)
                .OnDelete(DeleteBehavior.Cascade);
            encounter.HasMany(e => e.Participants)
                .WithOne(p => p.Encounter)
                .HasForeignKey(p => p.EncounterID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EncounterParticipant>(participant =>
        {
            // An NPC who leaves the party leaves the fight too
            participant.HasOne<Npc>()
                .WithMany()
                .HasForeignKey(p => p.NpcID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EncounterAction>(action =>
        {
            // An enemy taken out of the fight keeps its past actions, under the name it had
            action.HasOne<Enemy>()
                .WithMany()
                .HasForeignKey(a => a.EnemyID)
                .OnDelete(DeleteBehavior.SetNull);
            action.HasOne<Npc>()
                .WithMany()
                .HasForeignKey(a => a.NpcID)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MoveRequest>(request =>
        {
            // Asked from a fight, so they go with it
            request.HasOne(r => r.Encounter)
                .WithMany()
                .HasForeignKey(r => r.EncounterID)
                .OnDelete(DeleteBehavior.Cascade);
            request.HasOne<Enemy>()
                .WithMany()
                .HasForeignKey(r => r.EnemyID)
                .OnDelete(DeleteBehavior.SetNull);
            request.HasOne<Enemy>()
                .WithMany()
                .HasForeignKey(r => r.TargetEnemyID)
                .OnDelete(DeleteBehavior.SetNull);
            request.HasOne<Npc>()
                .WithMany()
                .HasForeignKey(r => r.TargetNpcID)
                .OnDelete(DeleteBehavior.SetNull);
            request.HasOne<EncounterAction>()
                .WithMany()
                .HasForeignKey(r => r.ActionID)
                .OnDelete(DeleteBehavior.SetNull);
            // The bot looks for those still waiting every second
            request.HasIndex(r => r.ResolvedAt);
        });

        modelBuilder.Entity<Enemy>(enemy =>
        {
            // Two fights can each have a "Goblin", but one fight can't have two
            enemy.HasIndex(e => new { e.EncounterID, e.Name }).IsUnique();
            enemy.Property(e => e.Name).UseCollation("NOCASE");
        });

        modelBuilder.Entity<MonsterTemplate>(monster =>
        {
            monster.HasIndex(m => m.Name).IsUnique();
            monster.Property(m => m.Name).UseCollation("NOCASE");
        });

        // An item is always in someone's inventory, so one taken out of it (Items.Remove) is deleted, not left behind
        // with no inventory
        modelBuilder.Entity<Inventory>(inventory =>
        {
            inventory.HasMany(i => i.Items)
                .WithOne()
                .HasForeignKey("InventoryID")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Weapons, armor and jars share the Item table, so their damage, armor and contents are saved with them
        modelBuilder.Entity<Weapon>();
        modelBuilder.Entity<Armor>();
        modelBuilder.Entity<HexedJars>();

        // A character's picture lives on their own row, but is loaded only when asked for (see CharacterPortrait)
        modelBuilder.Entity<Character>(character =>
        {
            character.HasOne(c => c.Portrait)
                .WithOne()
                .HasForeignKey<CharacterPortrait>(p => p.CharacterID);

            // Someone plays one character at a time
            character.HasIndex(c => c.ControlledByID).IsUnique();
        });

        modelBuilder.Entity<CharacterPortrait>(portrait =>
        {
            portrait.ToTable("Characters");
            portrait.HasKey(p => p.CharacterID);
            portrait.Property(p => p.CharacterID).HasColumnName("ID");
            portrait.Property(p => p.ContentType).HasColumnName("PortraitContentType").HasMaxLength(32);
            portrait.Property(p => p.Data).HasColumnName("PortraitData");
            portrait.Property(p => p.UpdatedAt).HasColumnName("PortraitUpdatedAt");
        });

        modelBuilder.Entity<RollRecord>(roll =>
        {
            roll.HasKey(r => r.MessageID);
            roll.HasIndex(r => r.UserID);
        });

        // A character's timeline goes with them
        modelBuilder.Entity<CharacterEvent>(e =>
        {
            e.HasOne<Character>()
                .WithMany()
                .HasForeignKey(c => c.CharacterID)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => c.CharacterID);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<Snowflake>().HaveConversion<SnowflakeConverter>();
        configurationBuilder.Properties<Snowflake?>().HaveConversion<NullableSnowflakeConverter>();
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<NullableUtcDateTimeOffsetConverter>();
    }
}