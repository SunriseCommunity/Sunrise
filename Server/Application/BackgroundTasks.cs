using System.IO.Compression;
using Hangfire;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Types.Enums;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

namespace Sunrise.Server.Application;

public static class BackgroundTasks
{
    public static void Initialize()
    {
        RecurringJob.AddOrUpdate("Backup database", () => BackupDatabase(), "0 3 * * *"); // 3 AM UTC

        RecurringJob.AddOrUpdate("Save stats snapshot", () => SaveStatsSnapshot(), "59 23 * * *"); // 11:59 PM UTC
    }

    public static async Task SaveStatsSnapshot()
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        foreach (var i in Enum.GetValues<GameMode>())
        {
            var usersStats = await database.UserService.Stats.GetAllUserStats(i, LeaderboardSortType.Pp);

            foreach (var stats in usersStats)
            {
                var currentSnapshot = await database.UserService.Stats.Snapshots.GetUserStatsSnapshot(stats.UserId, stats.GameMode);
                var rankSnapshots = currentSnapshot.GetSnapshots();

                rankSnapshots.Sort((a, b) => a.SavedAt.CompareTo(b.SavedAt));

                if (rankSnapshots.Count >= 70) rankSnapshots = rankSnapshots[..68];

                rankSnapshots.Add(new StatsSnapshot
                {
                    Rank = await database.UserService.Stats.GetUserRank(stats.UserId, stats.GameMode),
                    CountryRank = await database.UserService.Stats.GetUserCountryRank(stats.UserId, stats.GameMode),
                    PerformancePoints = stats.PerformancePoints
                });

                currentSnapshot.SetSnapshots(rankSnapshots);
                await database.UserService.Stats.Snapshots.UpdateUserStatsSnapshot(currentSnapshot);
            }
        }
    }

    public static void BackupDatabase()
    {
        const string filesPath = Configuration.DataPath;

        var databasePath = Path.Combine(filesPath, Configuration.DatabaseName);
        var backupDbPath = Path.Combine(filesPath, "Backup.db.tmp");

        var dataFolderPath = Path.Combine(filesPath, "Files");
        var backupPath = Path.Combine(filesPath, "Backups");

        if (!Directory.Exists(backupPath)) Directory.CreateDirectory(backupPath);

        var files = Directory.GetFiles(backupPath);

        if (files.Length >= Configuration.MaxDailyBackupCount)
        {
            var oldestFile = files.OrderBy(f => new FileInfo(f).CreationTime).First();
            File.Delete(oldestFile);
        }

        var zipFileName = Path.Combine(backupPath, $"Backup_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");

        var sourceFile = new FileInfo(databasePath);
        sourceFile.CopyTo(backupDbPath, true);


        using var zip = ZipFile.Open(zipFileName, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(backupDbPath, Configuration.DatabaseName);

        AddFolderToZip(zip, dataFolderPath, "Files");

        File.Delete(backupDbPath);
    }

    private static void AddFolderToZip(ZipArchive zip, string sourceDir, string entryRoot)
    {
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var entryName = Path.Combine(entryRoot, Path.GetFileName(file));
            zip.CreateEntryFromFile(file, entryName);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var entryName = Path.Combine(entryRoot, Path.GetFileName(dir));
            AddFolderToZip(zip, dir, entryName);
        }
    }
}