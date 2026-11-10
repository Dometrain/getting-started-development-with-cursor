using Microsoft.EntityFrameworkCore;

namespace LivePoll.Infrastructure.Persistence;

public sealed class LivePollDbContext(DbContextOptions<LivePollDbContext> options) : DbContext(options)
{
    public DbSet<PollRecord> Polls => Set<PollRecord>();

    public DbSet<PollOptionRecord> Options => Set<PollOptionRecord>();

    public DbSet<VoteRecord> Votes => Set<VoteRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PollRecord>(entity =>
        {
            entity.ToTable("polls");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Title).HasMaxLength(120).IsRequired();
            entity.Property(p => p.CreatedAt).IsRequired();
            entity.Property(p => p.EndsAt).IsRequired();
            entity.HasMany(p => p.Options)
                .WithOne(o => o.Poll)
                .HasForeignKey(o => o.PollId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(p => p.Votes)
                .WithOne(v => v.Poll)
                .HasForeignKey(v => v.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PollOptionRecord>(entity =>
        {
            entity.ToTable("poll_options");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Text).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<VoteRecord>(entity =>
        {
            entity.ToTable("votes");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.UserId).HasMaxLength(200).IsRequired();
            entity.Property(v => v.CastAt).IsRequired();
            entity.HasIndex(v => new { v.PollId, v.UserId }).IsUnique();
            entity.HasOne(v => v.Option)
                .WithMany()
                .HasForeignKey(v => v.OptionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}