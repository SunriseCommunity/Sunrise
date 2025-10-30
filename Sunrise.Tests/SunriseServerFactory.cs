using System.Data;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;

namespace Sunrise.Tests;

public class SunriseServerFactory : WebApplicationFactory<Server.Program>, IDisposable
{
    private static readonly SemaphoreSlim DbCleanupLock = new(1, 1);
    private static bool _schemaCreated;
    private static List<string>? _cachedTableNames;

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
                var redis = provider.GetRequiredService<RedisRepository>();
                MockHttpClient = new MockHttpClientService(redis);
                return MockHttpClient;
            });

            var isShouldCreateSchema = !_schemaCreated;

            if (!isShouldCreateSchema)
                return;

            DbCleanupLock.Wait();

            try
            {
                if (!isShouldCreateSchema)
                    return;

                using var scope = services.BuildServiceProvider().CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<SunriseDbContext>();

                if (!database.Database.GetDbConnection().Database.IsDatabaseForTesting())
                    throw new InvalidOperationException("Used database is not testing database. Are you trying to delete production data?");

                database.Database.EnsureDeleted();
                database.Database.EnsureCreated();

                _schemaCreated = true;
            }
            finally
            {
                DbCleanupLock.Release();
            }
        });
    }

    private async Task<List<string>> GetTableNamesAsync(SunriseDbContext db)
    {
        if (_cachedTableNames != null)
            return _cachedTableNames;

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        var tableNames = new List<string>();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT TABLE_NAME 
                FROM information_schema.TABLES 
                WHERE TABLE_SCHEMA = DATABASE() 
                AND TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_NAME";

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tableNames.Add(reader.GetString(0));
            }
        }

        _cachedTableNames = tableNames;
        return tableNames;
    }

    public async Task CleanupDatabaseAsync()
    {
        using var scope = Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SunriseDbContext>();

        if (Configuration.UseCache)
        {
            try
            {
                var redis = scope.ServiceProvider.GetRequiredService<RedisRepository>();
                await redis.Flush(flushOnlyGeneralDatabase: false);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to flush Redis cache.", ex);
            }
        }

        var tableNames = await GetTableNamesAsync(db);
        var nonEmptyTables = new List<string>();

        if (tableNames.Count == 0)
            return;

        var unionQuery = string.Join("\nUNION ALL\n",
            tableNames.Select(table =>
                $"SELECT '{table}' as table_name, EXISTS(SELECT 1 FROM `{table}` LIMIT 1) as has_data"));

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = unionQuery;

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString(0);
                var hasData = reader.GetInt32(1);

                if (hasData == 1)
                {
                    nonEmptyTables.Add(tableName);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to check tables for data existence: {ex.Message}", ex);
        }

        if (nonEmptyTables.Count > 0)
        {
            var batchQuery = "SET FOREIGN_KEY_CHECKS = 0;\n";

            foreach (var table in nonEmptyTables)
            {
                batchQuery += $"DELETE FROM `{table}`;\n";
            }

            batchQuery += "SET FOREIGN_KEY_CHECKS = 1;";

#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync(batchQuery);
#pragma warning restore EF1002
        }
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