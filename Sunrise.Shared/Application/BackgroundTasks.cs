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

public class BackgroundTasks
{
    public static void Initialize()
    {
        RecurringJob.AddOrUpdate("Backup database", () => BackupDatabase(CancellationToken.None), "0 3 * * *"); // 3 AM UTC

        RecurringJob.AddOrUpdate("Save stats snapshot", () => SaveStatsSnapshot(CancellationToken.None), "59 23 * * *"); // 11:59 PM UTC

        RecurringJob.AddOrUpdate("Disable inactive users", () => DisableInactiveUsers(CancellationToken.None), "0 1 * * *"); // 1 AM UTC
    }

    public static async Task SaveStatsSnapshot(CancellationToken ct)
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
                    var user = users.FirstOrDefault(u => u.Id == stats.UserId);
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
                    ct.ThrowIfCancellationRequested();
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

        var pageSize = 10;

        for (var i = 1;; i++)
        {
            var users = await database.Users.GetValidUsers(options: new QueryOptions(new Pagination(i, pageSize)));

            foreach (var user in users.Where(user => user.LastOnlineTime.AddDays(90) < DateTime.UtcNow))
            {
                ct.ThrowIfCancellationRequested();
                await database.Users.Moderation.DisableUser(user.Id);
            }

            if (users.Count < pageSize) break;
        }

    }

    public static async Task BackupDatabase(CancellationToken ct)
    {
        var dataFolderPath = Path.Combine(Configuration.DataPath, "Files");
        var backupPath = Path.Combine(Configuration.DataPath, "Backups");

        var databaseFilePath = Path.Combine(Configuration.DataPath, Configuration.DatabaseName);
        var databaseFileCopyPath = $"{databaseFilePath}.tmp";

        var zipFileName = Path.Combine(backupPath, $"Backup_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");

        try
        {
            if (!Directory.Exists(backupPath)) Directory.CreateDirectory(backupPath);

            var files = Directory.GetFiles(backupPath);

            if (files.Length >= Configuration.MaxDailyBackupCount)
            {
                var oldestFile = files.OrderBy(f => new FileInfo(f).CreationTime).First();
                File.Delete(oldestFile);
            }

            using var zipArchive = ZipFile.Open(zipFileName, ZipArchiveMode.Create);

            await CreateShadowCopy(databaseFilePath, databaseFileCopyPath, ct);

            await CopyFileToZip(zipArchive, databaseFilePath + ".tmp", Configuration.DatabaseName, ct);

            foreach (var filePath in GetFilesRecursively(dataFolderPath, ct))
            {
                ct.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(Configuration.DataPath, filePath);
                await CopyFileToZip(zipArchive, filePath, relativePath, ct);
            }
        }
        catch (Exception _)
        {
            if (File.Exists(zipFileName))
                File.Delete(zipFileName);

            throw;
        }
        finally
        {
            if (File.Exists(databaseFileCopyPath))
                File.Delete(databaseFileCopyPath);
        }
    }

    private static async Task CopyFileToZip(ZipArchive zipArchive, string path, string zipPath, CancellationToken ct = default)
    {
        var zipEntry = zipArchive.CreateEntry(zipPath);

        await using var fileReader = File.OpenRead(path);
        await using var zipStream = zipEntry.Open();
        await fileReader.CopyToAsync(zipStream, ct);
    }

    private static async Task CreateShadowCopy(string filePath, string newFilePath, CancellationToken ct)
    {
        await using var inputFile = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        await using var outputFile = new FileStream(newFilePath, FileMode.Create);

        var buffer = new byte[0x10000];
        int bytes;

        while ((bytes = await inputFile.ReadAsync(buffer, ct)) > 0)
        {
            await outputFile.WriteAsync(buffer.AsMemory(0, bytes), ct);
        }
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
            trySendMessage?.Invoke($"Error message: {ex.Message}");
            logger.LogError(ex, $"Exception occurred while executing job \"{jobName}\".");
        }
        finally
        {
            stopwatch.Stop();

            Configuration.OnMaintenance = false;
            trySendMessage?.Invoke($"Server is back online. Took time to proceed job \"{jobName}\": {stopwatch.ElapsedMilliseconds} ms.");
        }
    }
}