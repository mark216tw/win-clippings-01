using Clippings.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Clippings.Platform.Storage;

public sealed class CaptureEntity
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string ThumbnailPath { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }

    public string Source { get; set; } = string.Empty;
}

public sealed class ClipboardEntity
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public ClipboardItemKind Kind { get; set; }

    public string? TextContent { get; set; }

    public string? FilePath { get; set; }

    public string? ThumbnailPath { get; set; }

    public string Hash { get; set; } = string.Empty;

    public bool IsPinned { get; set; }
}

public sealed class ClippingsDbContext : DbContext
{
    public ClippingsDbContext(DbContextOptions<ClippingsDbContext> options)
        : base(options)
    {
    }

    public DbSet<CaptureEntity> Captures => Set<CaptureEntity>();

    public DbSet<ClipboardEntity> ClipboardItems => Set<ClipboardEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaptureEntity>(entity =>
        {
            entity.ToTable("Captures");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.CreatedAt);
            entity.HasIndex(item => item.Hash);
            entity.HasIndex(item => new { item.IsFavorite, item.CreatedAt })
                .HasDatabaseName("IX_Captures_IsFavorite_CreatedAt");
            entity.Property(item => item.FilePath).HasMaxLength(1024);
            entity.Property(item => item.ThumbnailPath).HasMaxLength(1024);
            entity.Property(item => item.Source).HasMaxLength(128);
        });

        modelBuilder.Entity<ClipboardEntity>(entity =>
        {
            entity.ToTable("ClipboardItems");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Hash).IsUnique();
            entity.HasIndex(item => item.CreatedAt);
            entity.HasIndex(item => new { item.IsPinned, item.CreatedAt })
                .HasDatabaseName("IX_ClipboardItems_IsPinned_CreatedAt");
            entity.Property(item => item.Hash).HasMaxLength(64);
        });
    }
}

