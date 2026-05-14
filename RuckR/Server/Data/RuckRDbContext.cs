using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RuckR.Shared.Models;

namespace RuckR.Server.Data
{
    public class RuckRDbContext : IdentityDbContext<IdentityUser>
    {
        public DbSet<PlayerModel> Players { get; set; }
        public DbSet<PitchModel> Pitches { get; set; }
        public DbSet<CollectionModel> Collections { get; set; }
        public DbSet<BattleModel> Battles { get; set; }
        public DbSet<UserGameProfileModel> UserGameProfiles { get; set; }
        public DbSet<PlayerEncounterModel> PlayerEncounters { get; set; }
        public DbSet<UserProfileModel> UserProfiles { get; set; }
        public DbSet<RateLimitRecord> RateLimitRecords { get; set; }
        public DbSet<UserConsent> UserConsents { get; set; }

        public RuckRDbContext(DbContextOptions<RuckRDbContext> options) : base(options) { }

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
                entity.HasIndex(e => e.ExpiresAtUtc);
                entity.HasOne(e => e.Player).WithMany().HasForeignKey(e => e.PlayerId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
