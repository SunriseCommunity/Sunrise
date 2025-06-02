using System.IO.Compression;
using CSharpFunctionalExtensions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Leaderboards;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Application;

public class RecurringJobs
{
    public static void Initialize()
    {
        RecurringJob.AddOrUpdate("Backup database", () => BackupDatabase(CancellationToken.None), "0 3 * * *"); // At 03:00 UTC

        RecurringJob.AddOrUpdate("Save users stats snapshots", () => SaveUsersStatsSnapshots(CancellationToken.None), "59 23 * * *"); // At 23:59 UTC

        RecurringJob.AddOrUpdate("Disable inactive users", () => DisableInactiveUsers(CancellationToken.None), "0 1 * * *"); // At 01:00 UTC

        RecurringJob.AddOrUpdate("Refresh users hypes", () => RefreshUsersHypes(CancellationToken.None), "0 0 * * 1"); // At 00:00 UTC on Monday
    }

    public static async Task SaveUsersStatsSnapshots(CancellationToken ct)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var pageSize = 100;

        foreach (var i in Enum.GetValues<GameMode>())
        {
            for (var x = 1;; x++)
            {
                var usersStats = await database.Users.Stats.GetUsersStats(i, LeaderboardSortType.Pp, options: new QueryOptions(true, new Pagination(x, pageSize)), ct: ct);

                var users = await database.Users.GetUsers(usersStats.Select(us => us.UserId).ToList(), ct: ct);

                foreach (var stats in usersStats)
                {
                    var user = users.FirstOrDefault(u => u.Id == stats.UserId);
                    if (user == null || !user.IsActive(false)) continue;

                    var currentSnapshot = await database.Users.Stats.Snapshots.GetUserStatsSnapshot(stats.UserId, stats.GameMode, ct);
                    var rankSnapshots = currentSnapshot.GetSnapshots();

                    rankSnapshots.Sort((a, b) => a.SavedAt.CompareTo(b.SavedAt));

                    if (rankSnapshots.Count >= 70) rankSnapshots = rankSnapshots[1..]; // Remove the oldest snapshot

                    var (globalRank, countryRank) = await database.Users.Stats.Ranks.GetUserRanks(user, stats.GameMode, ct: ct);

                    rankSnapshots.Add(new StatsSnapshot
                    {
                        Rank = globalRank,
                        CountryRank = countryRank,
                        PerformancePoints = stats.PerformancePoints
                    });

                    currentSnapshot.SetSnapshots(rankSnapshots);
                    await database.Users.Stats.Snapshots.UpdateUserStatsSnapshot(currentSnapshot);
                }

                if (usersStats.Count < pageSize) break;
            }
        }
    }

    public static async Task DisableInactiveUsers(CancellationToken ct)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var pageSize = 50;

        for (var i = 1;; i++)
        {
            var users = await database.Users.GetValidUsers(options: new QueryOptions(new Pagination(i, pageSize)), ct: ct);

            foreach (var user in users.Where(user => user.LastOnlineTime.AddDays(90) < DateTime.UtcNow))
            {
                ct.ThrowIfCancellationRequested();
                await database.Users.Moderation.DisableUser(user.Id);
            }

            if (users.Count < pageSize) break;
        }

    }

    public static async Task RefreshUsersHypes(CancellationToken ct)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var pageSize = 50;

        for (var i = 1;; i++)
        {
            var users = await database.Users.GetUsers(options: new QueryOptions(new Pagination(i, pageSize))
                {
                    QueryModifier = q => q.Cast<User>().Include(x => x.Inventory.Where(y => y.ItemType == ItemType.Hype))
                },
                ct: ct);

            foreach (var user in users.Where(user =>
                     {
                         var userHypes = user.Inventory.FirstOrDefault(x => x.ItemType == ItemType.Hype);

                         return userHypes == null || userHypes.Quantity < Configuration.UserHypesWeekly;
                     }))
            {
                ct.ThrowIfCancellationRequested();
                await database.Users.Inventory.SetInventoryItem(user, ItemType.Hype, Configuration.UserHypesWeekly);
            }

            if (users.Count < pageSize) break;
        }

    }

    public static async Task BackupDatabase(CancellationToken ct)
    {
        var dataFolderPath = Path.Combine(Configuration.DataPath, "Files");
        var backupPath = Path.Combine(Configuration.DataPath, "Backups");

        const string databaseBackupString = "backup_mysql_{0}_{1}.sql";
        string? backupDatabaseFilePath = null;

        var databaseBackupFilePath = Path.Combine(Configuration.DataPath, databaseBackupString);

        var zipFileName = Path.Combine(backupPath, $"Backup_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");

        try
        {
            if (!Directory.Exists(backupPath)) Directory.CreateDirectory(backupPath);

            var files = Directory.GetFiles(backupPath);

            if (Configuration.MaxDailyBackupCount == 0)
                return;

            if (files.Length >= Configuration.MaxDailyBackupCount)
            {
                var oldestFile = files.OrderBy(f => new FileInfo(f).CreationTime).First();
                File.Delete(oldestFile);
            }

            using var zipArchive = ZipFile.Open(zipFileName, ZipArchiveMode.Create);

            var backupDatabaseResult = await CreateDatabaseBackup(databaseBackupFilePath, ct);

            if (backupDatabaseResult.IsFailure)
                throw new Exception(backupDatabaseResult.Error);

            backupDatabaseFilePath = backupDatabaseResult.Value;
            var backupDatabaseFilename = Path.GetFileName(backupDatabaseFilePath);

            await CopyFileToZip(zipArchive, backupDatabaseFilePath, backupDatabaseFilename, ct);

            foreach (var filePath in GetFilesRecursively(dataFolderPath, ct))
            {
                ct.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(Configuration.DataPath, filePath);
                await CopyFileToZip(zipArchive, filePath, relativePath, ct);
            }
        }
        catch (Exception)
        {
            if (File.Exists(zipFileName))
                File.Delete(zipFileName);

            throw;
        }
        finally
        {
            if (backupDatabaseFilePath != null && File.Exists(backupDatabaseFilePath))
                File.Delete(backupDatabaseFilePath);
        }
    }

    private static async Task CopyFileToZip(ZipArchive zipArchive, string path, string zipPath, CancellationToken ct = default)
    {
        var zipEntry = zipArchive.CreateEntry(zipPath);

        await using var fileReader = File.OpenRead(path);
        await using var zipStream = zipEntry.Open();
        await fileReader.CopyToAsync(zipStream, ct);
    }

    private static async Task<Result<string>> CreateDatabaseBackup(string databaseBackupFilePathString, CancellationToken ct)
    {
        var backupDatabaseTask = Task.Run(async () =>
            {
                try
                {
                    await using var conn = new MySqlConnection(Configuration.DatabaseConnectionString);
                    await using var cmd = new MySqlCommand();
                    using var mb = new MySqlBackup(cmd);

                    var databaseBackupFilename = string.Format(databaseBackupFilePathString, mb.Database.Name, DateTime.UtcNow.ToString("yyyyMMddHHmmss"));

                    cmd.Connection = conn;
                    await conn.OpenAsync(ct);
                    mb.ExportToFile(databaseBackupFilename);
                    await conn.CloseAsync();

                    return databaseBackupFilename;
                }
                catch (Exception ex)
                {
                    return Result.Failure<string>(ex.Message);
                }
            },
            ct);

        if (await Task.WhenAny(backupDatabaseTask, Task.Delay(60_000, ct)) == backupDatabaseTask)
            return backupDatabaseTask.Result;

        return Result.Failure<string>("Database backup operation timed out");
    }

    private static IEnumerable<string> GetFilesRecursively(string directory, CancellationToken ct = default)
    {
        foreach (var filePath in Directory.GetFiles(directory))
        {
            ct.ThrowIfCancellationRequested();
            yield return filePath;
        }

        foreach (var subDirectory in Directory.GetDirectories(directory))
        {
            ct.ThrowIfCancellationRequested();

            foreach (var filePath in GetFilesRecursively(subDirectory, ct))
            {
                yield return filePath;
            }
        }
    }
}