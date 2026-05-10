using Hangfire;
using Sunrise.Processing.Scores.Jobs;

namespace Sunrise.Processing;

public static class ProcessingJobs
{
    public static void Initialize()
    {
        RecurringJob.AddOrUpdate<ScoreProcessingJob>("Process score queue", service => service.ProcessQueue(CancellationToken.None), Cron.Minutely);
    }
}