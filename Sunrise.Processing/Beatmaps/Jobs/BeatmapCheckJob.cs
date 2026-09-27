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

        var dueHashes = await database.Calculations.GetDueBeatmapChecks()
            .Take(CalculationRepository.BeatmapChecksPerTick)
            .ToListAsync(ct);

        foreach (var hashStatus in dueHashes)
        {
            if (!await CheckHash(database, beatmapService, hashStatus, ct))
                return;
        }
    }

    private static async Task<bool> CheckHash(DatabaseService database, BeatmapService beatmapService, BeatmapHashStatus hashStatus, CancellationToken ct)
    {
        var beatmapSetResult = await beatmapService.GetBeatmapSet(BaseSession.GenerateServerSession(), beatmapHash: hashStatus.BeatmapHash, useCache: false, ct: ct);

        if (beatmapSetResult.IsFailure && beatmapSetResult.Error.Status != HttpStatusCode.NotFound)
        {
            Log.Warning("Stopping beatmap checks for this tick, couldn't fetch hash {BeatmapHash}: {Error}", hashStatus.BeatmapHash, beatmapSetResult.Error.Message);
            return false;
        }

        var beatmap = beatmapSetResult.IsSuccess ? beatmapSetResult.Value.Beatmaps?.FirstOrDefault(b => b.Checksum == hashStatus.BeatmapHash) : null;

        hashStatus.CheckedAt = DateTime.UtcNow;

        if (beatmap != null)
        {
            await database.Calculations.ApplyHashStatus(hashStatus.BeatmapHash, beatmap.Id, beatmap.Status, ct);
            return true;
        }

        hashStatus.MissCount++;
        await database.DbContext.SaveChangesAsync(ct);

        if (hashStatus.MissCount >= 2 && hashStatus.Status != BeatmapStatus.NotSubmitted)
            await database.Calculations.ApplyHashStatus(hashStatus.BeatmapHash, hashStatus.BeatmapId, BeatmapStatus.NotSubmitted, ct);

        return true;
    }
}
