using CSharpFunctionalExtensions;
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

    public async Task<Result> CommitAsTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        var isCurrentlyInOtherTransactionScope = DbContext.Database.CurrentTransaction != null;
        await using var transaction = isCurrentlyInOtherTransactionScope ? null : await DbContext.Database.BeginTransactionAsync(ct);

        try
        {
            await action();
            await DbContext.SaveChangesAsync();

            if (!isCurrentlyInOtherTransactionScope && transaction != null)
                await transaction.CommitAsync(ct);

            return Result.Success();
        }
        catch (ApplicationException ex)
        {
            return Result.Failure($"{ex.Message}\n{ex.InnerException?.Message}");
        }
        catch (Exception ex)
        {
            if (!isCurrentlyInOtherTransactionScope && transaction != null)
                await transaction.RollbackAsync(ct);

            logger.LogWarning(ex, "Failed to process db transaction");

            return Result.Failure($"{ex.Message}\n{ex.InnerException?.Message}");
        }
    }
}