using System.Data.SQLite;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Repositories;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Repositories;
using BeatmapRepository = Sunrise.Shared.Database.Repositories.BeatmapRepository;

namespace Sunrise.Shared.Database;

public sealed class DatabaseService(
    ILogger<DatabaseService> logger,
    RedisRepository redis,
    SunriseDbContext dbContext,
    BeatmapRepository beatmapRepository,
    UserRepository userRepository,
    EventRepository eventRepository,
    ScoreRepository scoreRepository,
    MedalRepository medalRepository)
{

    public readonly BeatmapRepository Beatmaps = beatmapRepository;
    public readonly SunriseDbContext DbContext = dbContext;
    public readonly EventRepository Events = eventRepository;
    public readonly MedalRepository Medals = medalRepository;
    public readonly RedisRepository Redis = redis;
    public readonly ScoreRepository Scores = scoreRepository;
    public readonly UserRepository Users = userRepository;

    public async Task FlushAndUpdateRedisCache(bool isSoftFlush = true)
    {
        await Redis.Flush(isSoftFlush);

        logger.LogInformation("General cache (keys, db queries) flushed.");

        if (isSoftFlush)
            return;

        logger.LogInformation("All cache (sorted sets, keys, db queries) is flushed. Forced to rebuild user ranks.");

        var tasks = Enum.GetValues(typeof(GameMode))
            .Cast<GameMode>()
            .Select(async mode =>
            {
                using var scope = ServicesProviderHolder.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

                await database.Users.Stats.Ranks.SetAllUsersRanks(mode, 50);
            })
            .ToArray();

        await Task.WhenAll(tasks);

        logger.LogInformation("User ranks rebuilt. Sorted sets is now up to date.");
    }

    public async Task<Result> CommitAsTransactionAsync(Func<Task> action)
    {
        var isCurrentlyInOtherTransactionScope = DbContext.Database.CurrentTransaction != null;
        await using var transaction = isCurrentlyInOtherTransactionScope ? null : await DbContext.Database.BeginTransactionAsync();

        try
        {
            await action();
            await DbContext.SaveChangesAsync();

            if (!isCurrentlyInOtherTransactionScope && transaction != null)
                await transaction.CommitAsync();

            return Result.Success();
        }
        catch (ApplicationException ex)
        {
            return Result.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            if (!isCurrentlyInOtherTransactionScope && transaction != null)
                await transaction.RollbackAsync();

            logger.LogWarning(ex, "Failed to process db transaction");

            return Result.Failure($"{ex.Message}\n{ex.InnerException}\n{ex.StackTrace}");
        }
    }

    [Obsolete("This method executes old type of migrations, which would be removed in the future versions.")]
    public void CheckAndApplyOldTypeOfMigrations()
    {
        var connectionString = DbContext.Database.GetDbConnection().ConnectionString;

        using var conn = new SQLiteConnection(connectionString);

        conn.Open();

        var result = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='migration'", conn).ExecuteScalar();
        var isOldTypeOfMigrationTableExists = result != null;

        if (!isOldTypeOfMigrationTableExists)
        {
            conn.Close();
            return;
        }

        List<string> appliedMigrations = [];

        using (var reader = new SQLiteCommand("SELECT name FROM migration", conn).ExecuteReader())
        {
            while (reader.Read())
            {
                var migrationName = reader.GetString(0);
                appliedMigrations.Add(migrationName);
            }
        }

        var migrationFiles = Directory.GetFiles(Path.Combine(Configuration.DataPath, "Migrations"), "*.sql").OrderBy(f => f).ToList();

        var nonAppliedMigrations = migrationFiles.Where(f => !appliedMigrations.Contains(Path.GetFileName(f))).ToArray();

        using (var transaction = conn.BeginTransaction())
        {
            try
            {
                foreach (var filePath in nonAppliedMigrations)
                {
                    var sqlQuery = File.ReadAllText(filePath);
                    new SQLiteCommand(sqlQuery, conn, transaction).ExecuteNonQuery();

                    var insertAppliedMigrationCommand = new SQLiteCommand(
                        "INSERT INTO migration (Name, AppliedAt) VALUES (@MigrationName, @AppliedAt)",
                        conn,
                        transaction);

                    insertAppliedMigrationCommand.Parameters.AddWithValue("@MigrationName", Path.GetFileName(filePath));
                    insertAppliedMigrationCommand.Parameters.AddWithValue("@AppliedAt", DateTime.UtcNow);

                    insertAppliedMigrationCommand.ExecuteNonQuery();

                    Console.WriteLine($"Successfully executed migration: {filePath}");
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                logger.LogError($"Migration failed: {ex.Message}");
                transaction.Rollback();

                throw new Exception("Exception occured while applying migration.", ex);
            }
        }

        conn.Close();

        logger.LogInformation($"Successfully applied migrations: {nonAppliedMigrations.Length}");

        logger.LogInformation("Flushing all cache to avoid any data mismatching");

        FlushAndUpdateRedisCache(false).Wait();
    }
}