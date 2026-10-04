using System.Net;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Database.Repositories;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Processing.Beatmaps.Jobs;

public class BeatmapCheckJob(IServiceScopeFactory scopeFactory)
{
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    [AutomaticRetry(Attempts = 0)]
    public async Task Run(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        var beatmapService = scope.ServiceProvider.GetRequiredService<BeatmapService>();

        var dueBeatmapHashStatuses = await database.Calculations.GetDueBeatmapChecks()
            .Take(CalculationRepository.BeatmapChecksPerTick)
            .ToListAsync(ct);

        foreach (var beatmapHashStatus in dueBeatmapHashStatuses)
        {
            if (!await CheckBeatmapHashStatus(database, beatmapService, beatmapHashStatus, ct))
                return;
        }
    }

    private static async Task<bool> CheckBeatmapHashStatus(DatabaseService database, BeatmapService beatmapService, BeatmapHashStatus beatmapHashStatus, CancellationToken ct)
    {
        var beatmapSetResult = await beatmapService.GetBeatmapSet(BaseSession.GenerateServerSession(), beatmapHash: beatmapHashStatus.BeatmapHash, useCache: false, ct: ct);

        if (beatmapSetResult.IsFailure && beatmapSetResult.Error.Status != HttpStatusCode.NotFound)
        {
            Log.Warning("Stopping beatmap checks for this tick, couldn't fetch hash {BeatmapHash}: {Error}", beatmapHashStatus.BeatmapHash, beatmapSetResult.Error.Message);
            return false;
        }

        var beatmap = beatmapSetResult.IsSuccess ? beatmapSetResult.Value.Beatmaps?.FirstOrDefault(b => b.Checksum == beatmapHashStatus.BeatmapHash) : null;

        beatmapHashStatus.CheckedAt = DateTime.UtcNow;
        if (beatmap == null)
            beatmapHashStatus.MissCount++;

        await database.DbContext.SaveChangesAsync(ct);

        if (beatmap != null)
            await database.Calculations.ApplyBeatmapHashStatus(beatmapHashStatus.BeatmapHash, beatmap.Id, beatmap.Status, ct);
        else if (beatmapHashStatus.MissCount >= 2 && beatmapHashStatus.Status != BeatmapStatus.NotSubmitted)
            await database.Calculations.ApplyBeatmapHashStatus(beatmapHashStatus.BeatmapHash, beatmapHashStatus.BeatmapId, BeatmapStatus.NotSubmitted, ct);

        return true;
    }
}
