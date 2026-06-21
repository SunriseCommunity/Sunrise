using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Scores;

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
    public DbSet<EventScoreProcessing> EventScoreProcessings { get; set; }
    public DbSet<Restriction> Restrictions { get; set; }

    public DbSet<Score> Scores { get; set; }
    public DbSet<ScoreSubmissionRequest> ScoreSubmissionRequests { get; set; }
    public DbSet<ScoreProcessingTask> ScoreProcessingTasks { get; set; }

    public DbSet<BeatmapHype> BeatmapHypes { get; set; }
    public DbSet<CustomBeatmapStatus> CustomBeatmapStatuses { get; set; }



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        const string scoreTaskTypeColumn = nameof(ScoreProcessingTask.TaskType);
        const string scoreTaskScoreIdColumn = nameof(ScoreProcessingTask.ScoreId);
        const string scoreTaskPayloadIdColumn = nameof(ScoreProcessingTask.ScoreSubmissionRequestId);
        const string scoreTaskStatusColumn = nameof(ScoreProcessingTask.Status);

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

        modelBuilder.Entity<ScoreProcessingTask>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_score_processing_task_target",
                $"(({scoreTaskTypeColumn} = {(int)ScoreTaskType.Submission} AND {scoreTaskPayloadIdColumn} IS NOT NULL AND {scoreTaskScoreIdColumn} IS NULL) " +
                $"OR ({scoreTaskTypeColumn} <> {(int)ScoreTaskType.Submission} AND {scoreTaskPayloadIdColumn} IS NULL AND {scoreTaskScoreIdColumn} IS NOT NULL))"));

        modelBuilder.Entity<ScoreProcessingTask>()
            .Property<int?>("ActiveScoreId")
            .HasComputedColumnSql(
                $"CASE WHEN {scoreTaskStatusColumn} IN ({(int)ScoreProcessingStatus.Pending}, {(int)ScoreProcessingStatus.Processing}) THEN {scoreTaskScoreIdColumn} ELSE NULL END",
                true);

        modelBuilder.Entity<ScoreProcessingTask>()
            .Property<int?>("ActiveScoreSubmissionRequestId")
            .HasComputedColumnSql(
                $"CASE WHEN {scoreTaskStatusColumn} IN ({(int)ScoreProcessingStatus.Pending}, {(int)ScoreProcessingStatus.Processing}) THEN {scoreTaskPayloadIdColumn} ELSE NULL END",
                true);

        modelBuilder.Entity<ScoreProcessingTask>()
            .HasIndex("ActiveScoreId")
            .IsUnique()
            .HasDatabaseName("UX_score_processing_task_active_score");

        modelBuilder.Entity<ScoreProcessingTask>()
            .HasIndex("ActiveScoreSubmissionRequestId")
            .IsUnique()
            .HasDatabaseName("UX_score_processing_task_active_submission_request");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseMySql(Configuration.DatabaseConnectionString, ServerVersion.AutoDetect(Configuration.DatabaseConnectionString));
        }
    }
}