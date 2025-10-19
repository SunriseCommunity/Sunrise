using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Services;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;

namespace Sunrise.Tests;

public class SunriseServerFactory : WebApplicationFactory<Server.Program>, IDisposable
{
    public MockHttpClientService? MockHttpClient { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<SunriseDbContext>));
            if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

            services.AddDbContextPool<SunriseDbContext>((container, options) =>
            {
                options.EnableServiceProviderCaching(false);
                options.EnableSensitiveDataLogging();

                options.UseMySQL(Configuration.DatabaseConnectionString);
            });

            var httpClientDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(HttpClientService));
            if (httpClientDescriptor != null) services.Remove(httpClientDescriptor);

            services.AddScoped<HttpClientService>(provider =>
            {
                var redis = provider.GetRequiredService<Sunrise.Shared.Repositories.RedisRepository>();
                MockHttpClient = new MockHttpClientService(redis);
                return MockHttpClient;
            });

            using var scope = services.BuildServiceProvider().CreateScope();

            var database = scope.ServiceProvider.GetRequiredService<SunriseDbContext>();

            if (!database.Database.GetDbConnection().Database.IsDatabaseForTesting())
                throw new InvalidOperationException("Used database is not testing database. Are you trying to delete production data?");

            database.Database.EnsureDeleted();
            database.Database.EnsureCreated();
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