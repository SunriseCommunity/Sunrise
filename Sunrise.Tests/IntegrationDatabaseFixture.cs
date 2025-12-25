using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Database;
using Sunrise.Shared.Repositories;
using Sunrise.Tests.Manager;
using Sunrise.Tests.Utils;

namespace Sunrise.Tests;

public class IntegrationDatabaseFixture : IAsyncLifetime
{
    private readonly EnvironmentVariableManager _envManager = new();
    private string? _dataPathCopy;

    private readonly Dictionary<string, long> _seedMaxIds = new();

    public SunriseServerFactory App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _envManager.Set("ASPNETCORE_ENVIRONMENT", "Tests");
        _envManager.Set("Redis:ClearCacheOnStartup", "false");
        _envManager.Set("Redis:UseCache", "true");

        ConfigureCurrentDirectory();

        CreateFilesCopy();

        App = new SunriseServerFactory();

        using var scope = App.Server.Services.CreateScope();
        await InitialSeed(scope);
    }

    private async Task InitialSeed(IServiceScope scope)
    {
        var sessions = scope.ServiceProvider.GetRequiredService<SessionRepository>();

        using var seedScope = App.Server.Services.CreateScope();
        var db = seedScope.ServiceProvider.GetRequiredService<SunriseDbContext>();
        await DatabaseSeeder.UseAsyncSeeding(db);

        await Task.WhenAll(
            CaptureSeedState(scope),
            sessions.AddBotToSession()
        );
    }

    public async Task ResetAsync()
    {
        using var scope = App.Server.Services.CreateScope();

        await ClearSingletonState(scope);

        await Task.WhenAll(
            CleanupDatabaseAsync(scope),
            CleanupRedisAsync(scope),
            ReseedBotUserSession(scope)
        );
    }

    public async Task DisposeAsync()
    {
        await App.DisposeAsync();

        if (_dataPathCopy != null && Directory.Exists(_dataPathCopy))
        {
            try
            {
                Directory.Delete(_dataPathCopy, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete data path copy at {_dataPathCopy}: {ex.Message}");
            }
        }

        try
        {
            _envManager.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to dispose EnvironmentVariableManager: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    private static void ConfigureCurrentDirectory()
    {
        if (Directory.GetCurrentDirectory().Contains("bin"))
            Directory.SetCurrentDirectory(Path.Combine(Directory.GetCurrentDirectory(), "../../../../"));
    }

    private void CreateFilesCopy()
    {
        var projectRoot = Directory.GetCurrentDirectory();
        var sourcePath = Path.Combine(projectRoot, "Data.Tests");

        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Data.Tests folder not found at {sourcePath}. Current directory: {projectRoot}");
        }

        _dataPathCopy = Path.Combine(projectRoot, $"Data.Tests.{Guid.NewGuid():N}");

        _envManager.Set("Files:DataPath", _dataPathCopy);

        if (!Directory.Exists(_dataPathCopy))
            Directory.CreateDirectory(_dataPathCopy);

        FolderUtil.Copy(sourcePath, _dataPathCopy);
    }

    private async Task CleanupRedisAsync(IServiceScope scope)
    {
        try
        {
            var redis = scope.ServiceProvider.GetRequiredService<RedisRepository>();
            await redis.Flush(flushOnlyGeneralDatabase: false);
        }
        catch (Exception ex)
        {
            throw new Exception("Redis cleanup failed", ex);
        }
    }

    private async Task ClearSingletonState(IServiceScope scope)
    {
        var sessions = scope.ServiceProvider.GetRequiredService<SessionRepository>();
        var channels = scope.ServiceProvider.GetRequiredService<ChatChannelRepository>();

        var existingSessions = sessions.GetSessions().ToList();
        await Task.WhenAll(
            existingSessions.Select(session => sessions.RemoveSession(session)));

        var existingChannels = channels.GetChannels().ToList();
        Parallel.ForEach(existingChannels, channel =>
        {
            channels.RemoveAbstractChannel(channel.Name);
        });
    }

    private async Task ReseedBotUserSession(IServiceScope scope)
    {
        var sessions = scope.ServiceProvider.GetRequiredService<SessionRepository>();
        await sessions.AddBotToSession();
    }

    private async Task CaptureSeedState(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<SunriseDbContext>();
        var dbConnection = db.Database.GetDbConnection();

        if (dbConnection.State != System.Data.ConnectionState.Open)
            await dbConnection.OpenAsync();

        await using var command = dbConnection.CreateCommand();
        command.CommandText = @"
            SELECT TABLE_NAME, IFNULL(AUTO_INCREMENT - 1, 0) AS MaxId
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
            AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            var maxId = reader.GetInt64(1);
            _seedMaxIds[table] = maxId;
        }
    }

    private async Task CleanupDatabaseAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<SunriseDbContext>();

        var dbConnection = db.Database.GetDbConnection();
        if (dbConnection.State != System.Data.ConnectionState.Open)
            await dbConnection.OpenAsync();

        var tablesWithData = await GetNonEmptyTablesAsync(dbConnection, _seedMaxIds.Keys.ToList());

        var batchQuery = "SET FOREIGN_KEY_CHECKS = 0;\n";

        var tablesToProcess = _seedMaxIds
            .Where(kvp => kvp.Value > 0 || (kvp.Value == 0 && tablesWithData.Contains(kvp.Key)))
            .Select(kvp => kvp.Key)
            .ToDictionary(tableName => tableName, tableName => _seedMaxIds[tableName]);

        foreach (var (tableName, seedMaxId) in tablesToProcess)
        {
            batchQuery += $"DELETE FROM `{tableName}`{(seedMaxId == 0 ? "" : $"WHERE Id > {seedMaxId}")};\nALTER TABLE `{tableName}` AUTO_INCREMENT = 1;\n";
        }

        batchQuery += "SET FOREIGN_KEY_CHECKS = 1;";

        await using var command = dbConnection.CreateCommand();
        command.CommandText = batchQuery;
        await command.ExecuteNonQueryAsync();

        db.ChangeTracker.Clear();
    }

    private async Task<List<string>> GetNonEmptyTablesAsync(DbConnection db, List<string> tableNames)
    {
        var nonEmptyTables = new List<string>();

        var unionQuery = string.Join("\nUNION ALL\n",
            tableNames.Select(table =>
                $"SELECT '{table}' as table_name, EXISTS(SELECT 1 FROM `{table}` LIMIT 1) as has_data"));
        await using var command = db.CreateCommand();
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
        return nonEmptyTables;
    }
}
