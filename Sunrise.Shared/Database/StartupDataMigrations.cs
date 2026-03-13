using Hangfire;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Users;

namespace Sunrise.Shared.Database;

public static class StartupDataMigrations
{
    public static async Task Apply(DbContext context, CancellationToken ct = default)
    {
        await EnqueueRecalculateDisabledUsersStats(context, ct);
    }

    private static async Task EnqueueRecalculateDisabledUsersStats(DbContext context, CancellationToken ct = default)
    {
        var hasBrokenStats = await context.Set<UserStats>()
            .AnyAsync(us => us.PerformancePoints == -1 || us.Accuracy == -1, ct);

        if (!hasBrokenStats)
            return;

        BackgroundJob.Enqueue(() => RecurringJobs.RecalculateDisabledUsersStats(CancellationToken.None));
        Log.Information("Enqueued one-time job to recalculate broken disabled user stats");
    }
}
