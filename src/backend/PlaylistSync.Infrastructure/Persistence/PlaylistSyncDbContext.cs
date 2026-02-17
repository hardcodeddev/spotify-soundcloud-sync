using Microsoft.EntityFrameworkCore;
using PlaylistSync.Core;

namespace PlaylistSync.Infrastructure.Persistence;

public sealed class PlaylistSyncDbContext(DbContextOptions<PlaylistSyncDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<ConnectedAccount> ConnectedAccounts => Set<ConnectedAccount>();
    public DbSet<SyncProfile> SyncProfiles => Set<SyncProfile>();
    public DbSet<PlaylistMapping> PlaylistMappings => Set<PlaylistMapping>();
    public DbSet<SyncJob> SyncJobs => Set<SyncJob>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<OAuthState> OAuthStates => Set<OAuthState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("UserAccounts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ExternalUserId).IsUnique();
            entity.Property(x => x.ExternalUserId).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<ConnectedAccount>(entity =>
        {
            entity.ToTable("ConnectedAccounts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserAccountId, x.Provider }).IsUnique();
            entity.Property(x => x.Provider).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ProviderUserId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.AccessTokenRef).IsRequired();
            entity.Property(x => x.LastRefreshResult).HasMaxLength(100).IsRequired();
            entity.HasOne(x => x.UserAccount)
                .WithMany(x => x.ConnectedAccounts)
                .HasForeignKey(x => x.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyncProfile>(entity =>
        {
            entity.ToTable("SyncProfiles");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserAccountId).IsUnique();
            entity.HasOne(x => x.UserAccount)
                .WithOne(x => x.SyncProfile)
                .HasForeignKey<SyncProfile>(x => x.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlaylistMapping>(entity =>
        {
            entity.ToTable("PlaylistMappings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceProvider).HasMaxLength(50).IsRequired();
            entity.Property(x => x.SourcePlaylistId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TargetProvider).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TargetPlaylistId).HasMaxLength(200).IsRequired();
            entity.HasOne(x => x.SyncProfile)
                .WithMany(x => x.PlaylistMappings)
                .HasForeignKey(x => x.SyncProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyncJob>(entity =>
        {
            entity.ToTable("SyncJobs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserAccountId, x.RequestedIdempotencyKey }).IsUnique();
            entity.Property(x => x.RequestedIdempotencyKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Error).HasMaxLength(2000);
            entity.HasOne(x => x.UserAccount)
                .WithMany(x => x.SyncJobs)
                .HasForeignKey(x => x.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyncRun>(entity =>
        {
            entity.ToTable("SyncRuns");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Error).HasMaxLength(2000);
            entity.HasOne(x => x.SyncJob)
                .WithMany(x => x.Runs)
                .HasForeignKey(x => x.SyncJobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OAuthState>(entity =>
        {
            entity.ToTable("OAuthStates");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.State).IsUnique();
            entity.Property(x => x.Provider).HasMaxLength(50).IsRequired();
            entity.Property(x => x.UserId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.State).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CodeVerifier).HasMaxLength(200).IsRequired();
        });
    }
}
