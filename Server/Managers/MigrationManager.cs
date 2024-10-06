using Sunrise.Server.Database.Models;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Managers;

public class MigrationManager(WatsonORM orm)
{
    public void ApplyMigrations(string migrationsPath)
    {
        var appliedMigrations = orm.SelectMany<Migration>();
        var migrationFiles = Directory.GetFiles(migrationsPath, "*.sql").OrderBy(f => f).ToList();

        foreach (var file in migrationFiles)
        {
            var migrationName = Path.GetFileName(file);

            if (appliedMigrations.Exists(m => m.Name == migrationName)) continue;

            var sql = File.ReadAllText(file);
            orm.Query(sql);

            var historyEntry = new Migration
            {
                Name = migrationName,
                AppliedAt = DateTime.UtcNow
            };

            orm.Insert(historyEntry);
        }
    }
}