using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using PlaylistSync.Core;
using PlaylistSync.Infrastructure.Persistence;

#nullable disable

namespace PlaylistSync.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(PlaylistSyncDbContext))]
    partial class PlaylistSyncDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("PlaylistSync.Core.ConnectedAccount", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AccessTokenRef")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset?>("ExpiresAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("LastRefreshResult")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<DateTimeOffset?>("LastRefreshedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Provider")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("ProviderUserId")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("RefreshTokenRef")
                        .HasColumnType("text");

                    b.Property<Guid>("UserAccountId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserAccountId", "Provider")
                        .IsUnique();

                    b.ToTable("ConnectedAccounts", (string)null);
                });

            modelBuilder.Entity("PlaylistSync.Core.PlaylistMapping", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("SourcePlaylistId")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("SourceProvider")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<Guid>("SyncProfileId")
                        .HasColumnType("uuid");

                    b.Property<string>("TargetPlaylistId")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("TargetProvider")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.HasKey("Id");

                    b.HasIndex("SyncProfileId");

                    b.ToTable("PlaylistMappings", (string)null);
                });

            modelBuilder.Entity("PlaylistSync.Core.SyncJob", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset?>("EndedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Error")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)");

                    b.Property<string>("RequestedIdempotencyKey")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<DateTimeOffset>("StartedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<int>("TotalExportedCount")
                        .HasColumnType("integer");

                    b.Property<int>("TotalImportedCount")
                        .HasColumnType("integer");

                    b.Property<int>("TotalSkippedCount")
                        .HasColumnType("integer");

                    b.Property<Guid>("UserAccountId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserAccountId", "RequestedIdempotencyKey")
                        .IsUnique();

                    b.ToTable("SyncJobs", (string)null);
                });

            modelBuilder.Entity("PlaylistSync.Core.SyncProfile", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<int>("Direction")
                        .HasColumnType("integer");

                    b.Property<int>("LikesBehavior")
                        .HasColumnType("integer");

                    b.Property<string>("ScheduleCron")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<bool>("ScheduleEnabled")
                        .HasColumnType("boolean");

                    b.Property<string>("ScheduleTimeZone")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("UserAccountId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserAccountId")
                        .IsUnique();

                    b.ToTable("SyncProfiles", (string)null);
                });

            modelBuilder.Entity("PlaylistSync.Core.SyncRun", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset?>("EndedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Error")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)");

                    b.Property<int>("ExportedCount")
                        .HasColumnType("integer");

                    b.Property<int>("ImportedCount")
                        .HasColumnType("integer");

                    b.Property<int>("SkippedCount")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset>("StartedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<Guid>("SyncJobId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("SyncJobId");

                    b.ToTable("SyncRuns", (string)null);
                });

            modelBuilder.Entity("PlaylistSync.Core.UserAccount", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ExternalUserId")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.HasKey("Id");

                    b.HasIndex("ExternalUserId")
                        .IsUnique();

                    b.ToTable("UserAccounts", (string)null);
                });

            modelBuilder.Entity("PlaylistSync.Infrastructure.Persistence.OAuthState", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("CodeVerifier")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Provider")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("State")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.HasKey("Id");

                    b.HasIndex("State")
                        .IsUnique();

                    b.ToTable("OAuthStates", (string)null);
                });

            modelBuilder.Entity("PlaylistSync.Core.ConnectedAccount", b =>
                {
                    b.HasOne("PlaylistSync.Core.UserAccount", "UserAccount")
                        .WithMany("ConnectedAccounts")
                        .HasForeignKey("UserAccountId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("UserAccount");
                });

            modelBuilder.Entity("PlaylistSync.Core.PlaylistMapping", b =>
                {
                    b.HasOne("PlaylistSync.Core.SyncProfile", "SyncProfile")
                        .WithMany("PlaylistMappings")
                        .HasForeignKey("SyncProfileId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("SyncProfile");
                });

            modelBuilder.Entity("PlaylistSync.Core.SyncJob", b =>
                {
                    b.HasOne("PlaylistSync.Core.UserAccount", "UserAccount")
                        .WithMany("SyncJobs")
                        .HasForeignKey("UserAccountId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("UserAccount");
                });

            modelBuilder.Entity("PlaylistSync.Core.SyncProfile", b =>
                {
                    b.HasOne("PlaylistSync.Core.UserAccount", "UserAccount")
                        .WithOne("SyncProfile")
                        .HasForeignKey("PlaylistSync.Core.SyncProfile", "UserAccountId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("UserAccount");
                });

            modelBuilder.Entity("PlaylistSync.Core.SyncRun", b =>
                {
                    b.HasOne("PlaylistSync.Core.SyncJob", "SyncJob")
                        .WithMany("Runs")
                        .HasForeignKey("SyncJobId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("SyncJob");
                });
#pragma warning restore 612, 618
        }
    }
}
