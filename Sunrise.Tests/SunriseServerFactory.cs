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
        var manager = new RecurringJobManager();
        foreach (var job in JobStorage.Current.GetConnection().GetRecurringJobs())
        {
            manager.RemoveIfExists(job.Id);
        }

        using var connection = JobStorage.Current.GetConnection();

        foreach (var job in connection.GetAllItemsFromSet("processing-jobs"))
        {
            BackgroundJob.Delete(job);
        }
        
        foreach (var job in connection.GetAllItemsFromSet("scheduled-jobs"))
        {
            BackgroundJob.Delete(job);
        }

    }
}