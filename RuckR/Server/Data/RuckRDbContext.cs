using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RuckR.Shared.Models;

namespace RuckR.Server.Data
{
    /// <summary>Defines the server-side class RuckRDbContext.</summary>
    public class RuckRDbContext : IdentityDbContext<IdentityUser>
    {
        /// <summary>Gets or sets all players in the game.</summary>
        public DbSet<PlayerModel> Players { get; set; }
        /// <summary>Gets or sets all discovered and created pitches.</summary>
        public DbSet<PitchModel> Pitches { get; set; }
        /// <summary>Gets or sets all recruits collected by users.</summary>
        public DbSet<CollectionModel> Collections { get; set; }
        /// <summary>Gets or sets all battle records.</summary>
        public DbSet<BattleModel> Battles { get; set; }
        /// <summary>Gets or sets user game progress profiles.</summary>
        public DbSet<UserGameProfileModel> UserGameProfiles { get; set; }
        /// <summary>Gets or sets active recruitment encounters.</summary>
        public DbSet<PlayerEncounterModel> PlayerEncounters { get; set; }
        /// <summary>Gets or sets shared recruitment participants.</summary>
        public DbSet<RecruitmentParticipantModel> RecruitmentParticipants { get; set; }
        /// <summary>Gets or sets user visible profile records.</summary>
        public DbSet<UserProfileModel> UserProfiles { get; set; }
        /// <summary>Gets or sets all API rate limit records.</summary>
        public DbSet<RateLimitRecord> RateLimitRecords { get; set; }
        /// <summary>Gets or sets user consent records.</summary>
        public DbSet<UserConsent> UserConsents { get; set; }
        /// <summary>Gets or sets user recruitment item stacks.</summary>
        public DbSet<UserRecruitmentItemModel> UserRecruitmentItems { get; set; }
    /// <summary>Initializes a new instance of <see cref="RuckRDbContext"/>.</summary>
    /// <param name="options">The EF Core options.</param>
    public RuckRDbContext(DbContextOptions<RuckRDbContext> options) : base(options) { }
    /// <summary>Configure entities, indexes, and relationships for the model.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- PlayerModel ---
            modelBuilder.Entity<PlayerModel>(entity =>
            {
                entity.Property(p => p.SpawnLocation)
                    .HasColumnType("geography");
            });

            // --- PitchModel ---
            modelBuilder.Entity<PitchModel>(entity =>
            {
                entity.Property(p => p.Location)
                    .HasColumnType("geography");
                entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
                entity.Property(p => p.CreatorUserId).IsRequired();
                entity.Property(p => p.Source).IsRequired().HasMaxLength(50).HasDefaultValue("Manual");
                entity.Property(p => p.ExternalPlaceId).HasMaxLength(128);
                entity.Property(p => p.SourceCategory).HasMaxLength(200);
                entity.Property(p => p.SourceMatchReason).HasMaxLength(200);
                entity.HasIndex(p => p.ExternalPlaceId)
                    .IsUnique()
                    .HasFilter("[ExternalPlaceId] IS NOT NULL");
            });

            // --- CollectionModel ---
            modelBuilder.Entity<CollectionModel>(entity =>
            {
                entity.HasIndex(c => new { c.UserId, c.PlayerId }).IsUnique();
                entity.HasOne(c => c.Player).WithMany().HasForeignKey(c => c.PlayerId);
            });

            // --- BattleModel ---
            modelBuilder.Entity<BattleModel>(entity =>
            {
                entity.Property(b => b.RowVersion).IsRowVersion();
                entity.Property(b => b.ChallengerMove).HasConversion<string>().HasMaxLength(20);
                entity.Property(b => b.OpponentMove).HasConversion<string>().HasMaxLength(20);
                entity.Property(b => b.ResolutionMethod).HasMaxLength(200);
                entity.HasIndex(b => new { b.ChallengerId, b.Status, b.CreatedAt });
                entity.HasIndex(b => new { b.OpponentId, b.Status, b.CreatedAt });
                entity.HasOne<IdentityUser>().WithMany().HasForeignKey(b => b.ChallengerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<IdentityUser>().WithMany().HasForeignKey(b => b.OpponentId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<UserGameProfileModel>(entity =>
            {
                entity.HasKey(p => p.UserId);
                entity.Property(p => p.Level).HasDefaultValue(1);
                entity.Property(p => p.Experience).HasDefaultValue(0);
            });

            modelBuilder.Entity<UserProfileModel>(entity =>
            {
                entity.HasKey(p => p.UserId);
                entity.Property(p => p.Name).HasMaxLength(200);
                entity.Property(p => p.Biography).HasMaxLength(1000);
                entity.Property(p => p.Location).HasMaxLength(500);
                entity.Property(p => p.AvatarUrl).HasMaxLength(500);
            });

            modelBuilder.Entity<RateLimitRecord>(entity =>
            {
                entity.HasIndex(r => new { r.UserId, r.Action, r.TimestampUtc });
            });

            modelBuilder.Entity<PlayerEncounterModel>(entity =>
            {
                entity.HasIndex(e => new { e.UserId, e.PlayerId });
                entity.HasIndex(e => new { e.AreaKey, e.PlayerId });
                entity.HasIndex(e => new { e.AreaKey, e.ExpiresAtUtc });
                entity.HasIndex(e => e.ExpiresAtUtc);
                entity.Property(e => e.UserId).IsRequired(false);
                entity.Property(e => e.AreaKey).HasMaxLength(160);
                entity.Property(e => e.ParkPlaceId).HasMaxLength(128);
                entity.Property(e => e.RecruitmentItemKind).HasConversion<string>().HasMaxLength(20);
                entity.HasOne(e => e.Player).WithMany().HasForeignKey(e => e.PlayerId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<RecruitmentParticipantModel>(entity =>
            {
                entity.HasKey(p => new { p.EncounterId, p.UserId });
                entity.HasIndex(p => p.UserId);
                entity.HasIndex(p => p.CollectionAwardedAtUtc);
                entity.HasOne(p => p.Encounter)
                    .WithMany(e => e.Participants)
                    .HasForeignKey(p => p.EncounterId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserRecruitmentItemModel>(entity =>
            {
                entity.HasIndex(i => new { i.UserId, i.ItemKind }).IsUnique();
                entity.Property(i => i.ItemKind).HasConversion<string>().HasMaxLength(20);
                entity.Property(i => i.Quantity).HasDefaultValue(0);
            });
        }
    }
}

