using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Sunrise.Server.Tests;

internal class SunriseServerFactory : WebApplicationFactory<Program>
{
    public override async ValueTask DisposeAsync()
    {
        foreach (var factory in Factories)
        {
            await factory.DisposeAsync().ConfigureAwait(false);
        }

        var monitoringApi = JobStorage.Current.GetMonitoringApi();

        while (true)
        {
            var jobs = monitoringApi.ProcessingJobs(0, int.MaxValue);
            if (jobs.Count == 0)
                break;
        }

        using var connection = JobStorage.Current.GetConnection();

        foreach (var recurringJob in connection.GetRecurringJobs())
        {
            RecurringJob.RemoveIfExists(recurringJob.Id);
        }

        // TODO: Bad practice, should be handled!
        // We don't call base.DisposeAsync() to not dispose hangfire in memory database
        // ! I'm not sure if this is a good idea, but I 'think' it's fine for now
    }
}