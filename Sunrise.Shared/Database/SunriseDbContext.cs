using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Scores;
using ScoreTaskQueueEntity = Sunrise.Shared.Database.Models.Scores.ScoreTaskQueue;

namespace Sunrise.Shared.Database;

public class SunriseDbContext : DbContext
{
    public SunriseDbContext()
    {
    }

    public SunriseDbContext(DbContextOptions<SunriseDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<UserRelationship> UserRelationships { get; set; }
    public DbSet<UserFavouriteBeatmap> UserFavouriteBeatmaps { get; set; }
    public DbSet<UserMedals> UserMedals { get; set; }
    public DbSet<UserMetadata> UserMetadata { get; set; }
    public DbSet<UserStats> UserStats { get; set; }
    public DbSet<UserGrades> UserGrades { get; set; }
    public DbSet<UserStatsSnapshot> UserStatsSnapshot { get; set; }
    public DbSet<UserFile> UserFiles { get; set; }
    public DbSet<UserInventoryItem> UserInventoryItem { get; set; }


    public DbSet<Medal> Medals { get; set; }
    public DbSet<MedalFile> MedalFiles { get; set; }

    public DbSet<EventBeatmap> EventBeatmaps { get; set; }
    public DbSet<EventUser> EventUsers { get; set; }
    public DbSet<Restriction> Restrictions { get; set; }

    public DbSet<Score> Scores { get; set; }
    public DbSet<ScoreProcessingQueue> ScoreProcessingQueue { get; set; }
    public DbSet<ScoreTaskQueue> ScoreTaskQueue { get; set; }

    public DbSet<BeatmapHype> BeatmapHypes { get; set; }
    public DbSet<CustomBeatmapStatus> CustomBeatmapStatuses { get; set; }



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        const string scoreTaskTypeColumn = nameof(ScoreTaskQueueEntity.TaskType);
        const string scoreTaskScoreIdColumn = nameof(ScoreTaskQueueEntity.ScoreId);
        const string scoreTaskPayloadIdColumn = nameof(ScoreTaskQueueEntity.ScoreProcessingQueueId);
        const string scoreTaskStatusColumn = nameof(ScoreTaskQueueEntity.Status);

        modelBuilder.Entity<User>()
            .Property(u => u.Username)
            .UseCollation("utf8mb4_unicode_ci");

        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .UseCollation("utf8mb4_unicode_ci");

        modelBuilder.Entity<UserRelationship>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserInitiatedRelationships)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserRelationship>()
            .HasOne(ur => ur.Target)
            .WithMany(u => u.UserReceivedRelationships)
            .HasForeignKey(ur => ur.TargetId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ScoreTaskQueue>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_score_task_queue_target",
                $"(({scoreTaskTypeColumn} = {(int)ScoreTaskType.Submission} AND {scoreTaskPayloadIdColumn} IS NOT NULL AND {scoreTaskScoreIdColumn} IS NULL) " +
                $"OR ({scoreTaskTypeColumn} <> {(int)ScoreTaskType.Submission} AND {scoreTaskPayloadIdColumn} IS NULL AND {scoreTaskScoreIdColumn} IS NOT NULL))"));

        modelBuilder.Entity<ScoreTaskQueue>()
            .Property<int?>("ActiveScoreId")
            .HasComputedColumnSql(
                $"CASE WHEN {scoreTaskStatusColumn} IN ({(int)ScoreProcessingStatus.Pending}, {(int)ScoreProcessingStatus.Processing}) THEN {scoreTaskScoreIdColumn} ELSE NULL END",
                true);

        modelBuilder.Entity<ScoreTaskQueue>()
            .Property<int?>("ActiveScoreProcessingQueueId")
            .HasComputedColumnSql(
                $"CASE WHEN {scoreTaskStatusColumn} IN ({(int)ScoreProcessingStatus.Pending}, {(int)ScoreProcessingStatus.Processing}) THEN {scoreTaskPayloadIdColumn} ELSE NULL END",
                true);

        modelBuilder.Entity<ScoreTaskQueue>()
            .HasIndex("ActiveScoreId")
            .IsUnique()
            .HasDatabaseName("UX_score_task_queue_active_score");

        modelBuilder.Entity<ScoreTaskQueue>()
            .HasIndex("ActiveScoreProcessingQueueId")
            .IsUnique()
            .HasDatabaseName("UX_score_task_queue_active_payload");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseMySQL(Configuration.DatabaseConnectionString);
        }
    }
}