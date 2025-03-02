using System.Data.Common;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;

namespace Sunrise.Tests;

public class SunriseServerFactory : WebApplicationFactory<Server.Program>, IDisposable
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<SunriseDbContext>));
            if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

            services.AddSingleton<DbConnection>(_ =>
            {
                var connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();

                return connection;
            });

            services.AddDbContextPool<SunriseDbContext>((container, options) =>
            {
                options.EnableServiceProviderCaching(false);
                options.EnableSensitiveDataLogging();

                var connection = container.GetRequiredService<DbConnection>();
                options.UseSqlite(connection).UseSeeding((ctx, _) => { DatabaseSeeder.UseAsyncSeeding(ctx).Wait(); });
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing == false)
        {
            base.Dispose(disposing);
            return;
        }
        
        var mon = JobStorage.Current.GetMonitoringApi();
        var scheduledJobs = mon.ScheduledJobs(0, int.MaxValue);
        var jobs = scheduledJobs.ToList();
        jobs.ForEach(x => BackgroundJob.Delete(x.Key));
        
        base.Dispose(disposing);
    }
}