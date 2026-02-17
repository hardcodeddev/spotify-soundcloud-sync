using Microsoft.EntityFrameworkCore;

namespace PlaylistSync.Infrastructure.Persistence;

public sealed class PlaylistSyncDbContext(DbContextOptions<PlaylistSyncDbContext> options) : DbContext(options)
{
    public DbSet<ConnectedAccount> ConnectedAccounts => Set<ConnectedAccount>();
    public DbSet<OAuthState> OAuthStates => Set<OAuthState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConnectedAccount>(entity =>
        {
            entity.ToTable("ConnectedAccounts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.Provider }).IsUnique();
            entity.Property(x => x.UserId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Provider).HasMaxLength(50).IsRequired();
            entity.Property(x => x.EncryptedAccessToken).IsRequired();
            entity.Property(x => x.LastRefreshResult).HasMaxLength(100).IsRequired();
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
