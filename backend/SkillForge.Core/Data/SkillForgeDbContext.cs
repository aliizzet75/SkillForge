using Microsoft.EntityFrameworkCore;
using SkillForge.Core.Models;

namespace SkillForge.Core.Data;

public class SkillForgeDbContext : DbContext
{
    public SkillForgeDbContext(DbContextOptions<SkillForgeDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserSkill> UserSkills => Set<UserSkill>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<GameRound> GameRounds => Set<GameRound>();
    public DbSet<MatchHistory> MatchHistories => Set<MatchHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User indexes
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.CountryCode);

        // UserSkill composite index
        modelBuilder.Entity<UserSkill>()
            .HasIndex(us => new { us.SkillType, us.Percentile });

        modelBuilder.Entity<UserSkill>()
            .HasIndex(us => new { us.SkillType, us.XP });

        // GameSession relationships
        modelBuilder.Entity<GameSession>()
            .HasOne(gs => gs.Player1)
            .WithMany(u => u.GameSessionsAsPlayer1)
            .HasForeignKey(gs => gs.Player1Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GameSession>()
            .HasOne(gs => gs.Player2)
            .WithMany(u => u.GameSessionsAsPlayer2)
            .HasForeignKey(gs => gs.Player2Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GameSession>()
            .HasIndex(gs => gs.Status);

        modelBuilder.Entity<GameSession>()
            .HasIndex(gs => new { gs.Player1Id, gs.Player2Id });

        // GameRound relationship
        modelBuilder.Entity<GameRound>()
            .HasOne(gr => gr.Session)
            .WithMany(gs => gs.Rounds)
            .HasForeignKey(gr => gr.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GameRound>()
            .HasIndex(gr => gr.SessionId);

        // JSON columns for PostgreSQL
        modelBuilder.Entity<GameSession>()
            .Property(gs => gs.Player1SkillDelta)
            .HasColumnType("jsonb");

        modelBuilder.Entity<GameSession>()
            .Property(gs => gs.Player2SkillDelta)
            .HasColumnType("jsonb");

        modelBuilder.Entity<GameSession>()
            .Property(gs => gs.DisconnectionInfo)
            .HasColumnType("jsonb");

        modelBuilder.Entity<GameRound>()
            .Property(gr => gr.Player1Data)
            .HasColumnType("jsonb");

        modelBuilder.Entity<GameRound>()
            .Property(gr => gr.Player2Data)
            .HasColumnType("jsonb");
    }
}
