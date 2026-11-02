using Hangfire;
using Sunrise.Processing.Beatmaps.Jobs;
using Sunrise.Processing.Calculations.Jobs;

namespace Sunrise.Processing;

public static class ProcessingJobs
{
    public static void Initialize()
    {
        RecurringJob.RemoveIfExists("Process score queue");
        RecurringJob.AddOrUpdate<CalculationRunJob>("Advance calculation run", service => service.Run(CancellationToken.None), Cron.Minutely);
        RecurringJob.AddOrUpdate<BeatmapCheckJob>("Check beatmap hashes", service => service.Run(CancellationToken.None), Cron.Minutely);
    }
}
