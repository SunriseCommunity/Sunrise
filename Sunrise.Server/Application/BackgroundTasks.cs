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

        RecurringJob.AddOrUpdate("Disable inactive users", () => DisableInactiveUsers(), "0 1 * * *"); // 1 AM UTC
    }

    public static async Task SaveStatsSnapshot()
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        foreach (var i in Enum.GetValues<GameMode>())
        {
            var usersStats = await database.UserService.Stats.GetAllUserStats(i, LeaderboardSortType.Pp);

            var users = await database.UserService.GetAllUsers();

            foreach (var stats in usersStats)
            {
                var user = users.FirstOrDefault(x => x.Id == stats.UserId);
                if (user == null || !user.IsActive(false)) continue;

                var currentSnapshot = await database.UserService.Stats.Snapshots.GetUserStatsSnapshot(stats.UserId, stats.GameMode);
                var rankSnapshots = currentSnapshot.GetSnapshots();

                rankSnapshots.Sort((a, b) => a.SavedAt.CompareTo(b.SavedAt));

                if (rankSnapshots.Count >= 70) rankSnapshots = rankSnapshots[1..]; // Remove the oldest snapshot

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

    public static async Task DisableInactiveUsers()
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var users = await database.UserService.GetAllUsers();
        if (users == null) return;

        foreach (var user in users.Where(user => user.LastOnlineTime.AddDays(90) < DateTime.UtcNow))
        {
            await database.UserService.Moderation.DisableUser(user.Id);
        }
    }

    public static void BackupDatabase()
    {
        var filesPath = Configuration.DataPath;

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