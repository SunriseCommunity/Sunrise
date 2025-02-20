using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Models;
using Watson.ORM.Sqlite;

namespace Sunrise.Shared.Helpers;

public class MigrationManager
{
    private readonly ILogger<MigrationManager> _logger;
    private readonly WatsonORM _orm;

    public MigrationManager(WatsonORM orm)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<MigrationManager>();
        _orm = orm;
    }

    public int ApplyMigrations(string migrationsPath)
    {
        var appliedMigrations = _orm.SelectMany<Migration>();
        var migrationFiles = Directory.GetFiles(migrationsPath, "*.sql").OrderBy(f => f).ToList();

        var migrationsApplied = 0;

        foreach (var file in migrationFiles)
        {
            var migrationName = Path.GetFileName(file);

            if (appliedMigrations.Exists(m => m.Name == migrationName)) continue;

            var sql = File.ReadAllText(file);

            _logger.LogInformation($"Applying migration: {migrationName}");

            _orm.Query(sql);

            var historyEntry = new Migration
            {
                Name = migrationName,
                AppliedAt = DateTime.UtcNow
            };

            _orm.Insert(historyEntry);

            migrationsApplied++;
        }

        return migrationsApplied;
    }
}