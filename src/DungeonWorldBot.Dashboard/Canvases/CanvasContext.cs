using DungeonWorldBot.Dashboard.Party;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Canvases;

/// <summary>
/// The dashboard's own database, for canvases and characters' pictures. Kept apart from the bot's, which the dashboard
/// only ever reads (bar a few things it writes on purpose).
/// </summary>
public class CanvasContext(DbContextOptions<CanvasContext> options) : DbContext(options)
{
    public DbSet<Canvas> Canvases => Set<Canvas>();

    public DbSet<CanvasItem> CanvasItems => Set<CanvasItem>();

    public DbSet<CanvasImage> CanvasImages => Set<CanvasImage>();

    public DbSet<CanvasThumbnail> CanvasThumbnails => Set<CanvasThumbnail>();

    public DbSet<CharacterPortrait> CharacterPortraits => Set<CharacterPortrait>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Canvas>(canvas =>
        {
            canvas.Property(c => c.Name).HasMaxLength(CanvasStore.MaxNameLength);
            canvas.Property(c => c.Kind).HasConversion<string>();
            canvas.HasIndex(c => c.UpdatedAt);
        });

        modelBuilder.Entity<CanvasItem>(item =>
        {
            // Pages make the IDs, so they're only unique within a canvas
            item.HasKey(i => new { i.CanvasID, i.ID });
            item.Property(i => i.ID).HasMaxLength(CanvasStore.MaxItemIDLength);
            item.Property(i => i.Type).HasConversion<string>();
            item.HasOne(i => i.Canvas).WithMany(c => c.Items).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CanvasImage>(image =>
        {
            image.Property(i => i.ID).HasMaxLength(32);
            image.Property(i => i.ContentType).HasMaxLength(32);
            image.HasOne(i => i.Canvas).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CanvasThumbnail>(thumbnail =>
        {
            thumbnail.HasKey(t => t.CanvasID);
            thumbnail.Property(t => t.ContentType).HasMaxLength(32);
            thumbnail.HasOne(t => t.Canvas).WithOne().HasForeignKey<CanvasThumbnail>(t => t.CanvasID).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterPortrait>(portrait =>
        {
            portrait.HasKey(p => p.CharacterID);
            // Discord IDs are handed out, not counted up
            portrait.Property(p => p.CharacterID).ValueGeneratedNever();
            portrait.Property(p => p.ContentType).HasMaxLength(32);
        });
    }
}
