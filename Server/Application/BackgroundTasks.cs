using System.IO.Compression;
using Hangfire;
using osu.Shared;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Types.Enums;

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
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        for (var i = 0; i < 4; i++)
        {
            var usersStats = await database.GetAllUserStats((GameMode)i, LeaderboardSortType.Pp);

            foreach (var stats in usersStats)
            {
                var currentSnapshot = await database.GetUserStatsSnapshot(stats.UserId, stats.GameMode);
                var rankSnapshots = currentSnapshot.GetSnapshots();

                rankSnapshots.Sort((a, b) => a.SavedAt.CompareTo(b.SavedAt));

                if (rankSnapshots.Count >= 70) rankSnapshots = rankSnapshots[..68];

                rankSnapshots.Add(new StatsSnapshot
                {
                    Rank = await database.GetUserRank(stats.UserId, stats.GameMode),
                    CountryRank = await database.GetUserCountryRank(stats.UserId, stats.GameMode),
                    PerformancePoints = stats.PerformancePoints
                });

                currentSnapshot.SetSnapshots(rankSnapshots);
                await database.UpdateUserStatsSnapshot(currentSnapshot);
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

        AddFolderToZip(zip, dataFolderPath, "Data");

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