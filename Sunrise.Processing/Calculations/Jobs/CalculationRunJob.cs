using EFCoreSecondLevelCacheInterceptor;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Processing.Calculations.Jobs;

public class CalculationRunJob(IServiceScopeFactory scopeFactory)
{
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    [AutomaticRetry(Attempts = 0)]
    public async Task Run(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        var calculatorService = scope.ServiceProvider.GetRequiredService<CalculatorService>();

        var targetResult = await calculatorService.GetTargetRosuVersion(BaseSession.GenerateServerSession(), ct);

        if (targetResult.IsFailure)
        {
            Log.Warning("Skipping calculation run tick, couldn't get calculator version: {Error}", targetResult.Error.Message);
            return;
        }

        var targetVersionId = (await database.Calculations.GetOrCreateVersionId(targetResult.Value, ct))!.Value;

        var run = await database.Calculations.GetLastRun(ct);

        if (run == null || run.TargetVersionId != targetVersionId)
        {
            Log.Information("Starting calculation run for rosu version {RosuVersion}", targetResult.Value);
            run = await database.Calculations.StartRun(run is { FinishedAt: null } ? run : null, targetVersionId, ct: ct);
        }

        if (run.FinishedAt != null)
            return;

        if (run.Phase == CalculationRunPhase.Enqueue)
        {
            await database.Calculations.EnqueueRunScores(run, ct);
            run.Phase = CalculationRunPhase.Scores;
            await database.DbContext.SaveChangesAsync(ct);
        }

        if (run.Phase == CalculationRunPhase.Scores)
        {
            if (await database.Calculations.HasActiveRunTasks(run, ct) || !run.IsForced && await database.Calculations.EnqueueRunScores(run, ct) > 0)
                return;

            run.Phase = CalculationRunPhase.UserStats;
            await database.DbContext.SaveChangesAsync(ct);
        }

        if (run.Phase == CalculationRunPhase.UserStats)
        {
            // During this phase profiles may show new pp while the leaderboard still shows old ranks. This is somewhat acceptable.
            await RecalculateAllUserStats(ct);
            run.Phase = CalculationRunPhase.Leaderboard;
            await database.DbContext.SaveChangesAsync(ct);
        }

        if (run.Phase == CalculationRunPhase.Leaderboard)
        {
            foreach (var mode in Enum.GetValues<GameMode>())
            {
                await database.Users.Stats.Ranks.RebuildAllUsersRanks(mode);
            }

            run.Phase = CalculationRunPhase.Finished;
            run.FinishedAt = DateTime.UtcNow;
            await database.DbContext.SaveChangesAsync(ct);

            Log.Information("Finished calculation run {RunId}", run.Id);
        }
    }

    private async Task RecalculateAllUserStats(CancellationToken ct)
    {
        var lastId = 0;

        while (true)
        {
            using var scope = scopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
            var calculatorService = scope.ServiceProvider.GetRequiredService<CalculatorService>();

            var page = await database.DbContext.UserStats.NotCacheable()
                .Include(s => s.User)
                .Where(s => s.Id > lastId)
                .OrderBy(s => s.Id)
                .Take(500)
                .ToListAsync(ct);

            if (page.Count == 0)
                return;

            foreach (var stats in page)
            {
                (stats.PerformancePoints, stats.Accuracy) = await calculatorService.CalculateUserWeightedStats(stats.User, stats.GameMode);
            }

            await database.DbContext.SaveChangesAsync(ct);
            lastId = page[^1].Id;
        }
    }
}