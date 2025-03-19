using System.Diagnostics;
using System.IO.Compression;
using System.Linq.Expressions;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Repositories;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Application;

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
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var pageSize = 100;

        foreach (var i in Enum.GetValues<GameMode>())
        {
            for (var x = 1;; x++)
            {
                var usersStats = await database.Users.Stats.GetUsersStats(i, LeaderboardSortType.Pp, options: new QueryOptions(true, new Pagination(x, pageSize)));

                var users = await database.Users.GetUsers(usersStats.Select(us => us.UserId).ToList());

                foreach (var stats in usersStats)
                {
                    var user = users.FirstOrDefault(x => x.Id == stats.UserId);
                    if (user == null || !user.IsActive(false)) continue;

                    var currentSnapshot = await database.Users.Stats.Snapshots.GetUserStatsSnapshot(stats.UserId, stats.GameMode);
                    var rankSnapshots = currentSnapshot.GetSnapshots();

                    rankSnapshots.Sort((a, b) => a.SavedAt.CompareTo(b.SavedAt));

                    if (rankSnapshots.Count >= 70) rankSnapshots = rankSnapshots[1..]; // Remove the oldest snapshot

                    var (globalRank, countryRank) = await database.Users.Stats.Ranks.GetUserRanks(user, stats.GameMode);

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

    public static async Task DisableInactiveUsers()
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var pageSize = 10;

        for (var i = 1;; i++)
        {
            var users = await database.Users.GetValidUsers(options: new QueryOptions(new Pagination(i, pageSize)));

            foreach (var user in users.Where(user => user.LastOnlineTime.AddDays(90) < DateTime.UtcNow))
            {
                await database.Users.Moderation.DisableUser(user.Id);
            }

            if (users.Count < pageSize) break;
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

    public static void TryStartNewBackgroundJob<T>(
        Expression<Func<Task>> action,
        Action<string>? trySendMessage,
        bool? shouldEnterMaintenance = false)
    {
        if (Configuration.OnMaintenance && shouldEnterMaintenance == true)
        {
            trySendMessage?.Invoke("Server is in maintenance mode. Starting new jobs which requires server to enter maintenance mode is not possible.");
            return;
        }

        var jobName = typeof(T).Name;

        trySendMessage?.Invoke($"{jobName} has been started.");

        if (shouldEnterMaintenance == true)
        {
            Configuration.OnMaintenance = true;

            trySendMessage?.Invoke("Server will enter maintenance mode until it's done.");

            var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

            foreach (var userSession in sessions.GetSessions())
            {
                userSession.SendBanchoMaintenance();
            }
        }

        var jobId = BackgroundJob.Enqueue(action);

        trySendMessage?.Invoke($"Use '{Configuration.BotPrefix}canceljob {jobId}' to stop the {jobName} execution.");
    }

    public static async Task ExecuteBackgroundTask<T>(
        Func<Task> action,
        Action<string>? trySendMessage = null)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<T>>();

        var stopwatch = Stopwatch.StartNew();
        var jobName = typeof(T).Name;

        try
        {
            await action();

            trySendMessage?.Invoke($"{jobName} has finished in {stopwatch.ElapsedMilliseconds} ms.");
        }
        catch (OperationCanceledException)
        {
            trySendMessage?.Invoke($"{jobName} was stopped.");
            logger.LogInformation($"{jobName} was stopped by user.");
        }
        catch (Exception ex)
        {
            trySendMessage?.Invoke($"Error occurred while executing {jobName}. Check console for more details.");
            logger.LogError(ex, $"Exception occurred while executing job \"{jobName}\".");
        }
        finally
        {
            stopwatch.Stop();

            Configuration.OnMaintenance = false;
            trySendMessage?.Invoke($"Server is back online. Took time to proceed job \"{jobName}\": {stopwatch.ElapsedMilliseconds} ms.");
        }
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