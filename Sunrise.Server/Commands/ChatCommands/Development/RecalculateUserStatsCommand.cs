using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("recalculateuserstats", requiredPrivileges: UserPrivilege.Developer)]
public class RecalculateUserStatsCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 2)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}recalculateuserstats <modeEnum | all> <isStartMaintenance> | Example: {Configuration.BotPrefix}recalculateuserstats 0 true for osu std calculation with maintenance mode on.");
            return Task.CompletedTask;
        }

        GameMode? mode = Enum.TryParse(args[0], out GameMode parsedMode) ? parsedMode : null;

        if (mode == null && args[0] != "all")
        {
            ChatCommandRepository.SendMessage(session, "Invalid mode.");
            return Task.CompletedTask;
        }

        if (!bool.TryParse(args[1], out var isStartMaintenance))
        {
            ChatCommandRepository.SendMessage(session, "Invalid isStartMaintenance value.");
            return Task.CompletedTask;
        }

        BackgroundTaskService.TryStartNewBackgroundJob<RecalculateUserStatsCommand>(
            () => RecalculateUserStats(session.UserId, CancellationToken.None, mode),
            message => ChatCommandRepository.TrySendMessage(session.UserId, message),
            isStartMaintenance);

        return Task.CompletedTask;
    }

    public async Task RecalculateUserStats(int userId, CancellationToken token, GameMode? mode = null)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<RecalculateUserStatsCommand>(
            async () =>
            {
                if (!mode.HasValue)
                {
                    foreach (var gameMode in Enum.GetValues<GameMode>())
                    {
                        await RecalculateUserStatsInGamemode(userId, token, gameMode);
                    }
                }
                else
                {
                    await RecalculateUserStatsInGamemode(userId, token, mode.Value);
                }

                ChatCommandRepository.TrySendMessage(userId, "Recalculation user stats has finished");
            },
            message => ChatCommandRepository.TrySendMessage(userId, message)
        );
    }

    public async Task RecalculateUserStatsInGamemode(int userId, CancellationToken token, GameMode mode)
    {
        var startTime = DateTime.UtcNow;

        var pageSize = 50;

        for (var x = 1;; x++)
        {
            using var scope = ServicesProviderHolder.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
            var calculatorService = scope.ServiceProvider.GetRequiredService<CalculatorService>();

            var pageStats = await database.Users.Stats.GetUsersStats(mode, LeaderboardSortType.Pp, options: new QueryOptions(new Pagination(x, pageSize)));

            foreach (var stats in pageStats)
            {
                await UpdateUserStats(stats, calculatorService, database, userId);
                token.ThrowIfCancellationRequested();
            }

            if (pageStats.Count < pageSize) break;
        }

        ChatCommandRepository.TrySendMessage(userId,
            $"Recalculated stats for mode {mode}. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");
    }

    private async Task UpdateUserStats(UserStats stats, CalculatorService calculatorService, DatabaseService database, int userIdToSendUpdatesTo)
    {
        var tempUserStats = new UserStats
        {
            GameMode = stats.GameMode
        };

        var pageSize = 100;

        for (var i = 1;; i++)
        {
            var (pageScores, totalScores) = await database.Scores.GetUserScores(stats.UserId, stats.GameMode, ScoreTableType.Recent, new QueryOptions(new Pagination(i, pageSize)));

            if (totalScores == 0)
            {
                return;
            }

            foreach (var score in pageScores)
            {
                tempUserStats.UpdateWithDbScore(score);
            }

            if (pageScores.Count < pageSize) break;
        }

        var pp = await calculatorService.CalculateUserWeightedPerformance(stats.UserId, stats.GameMode);
        var acc = await calculatorService.CalculateUserWeightedAccuracy(stats.UserId, stats.GameMode);

        const float tolerance = 0.0001f;

        if (Math.Abs(stats.PerformancePoints - pp) > tolerance)
            ChatCommandRepository.TrySendMessage(userIdToSendUpdatesTo, $"Updated user id {stats.UserId} in gamemode {stats.GameMode} pp value from {stats.PerformancePoints} -> {pp}");

        if (Math.Abs(stats.Accuracy - acc) > tolerance)
            ChatCommandRepository.TrySendMessage(userIdToSendUpdatesTo, $"Updated user id {stats.UserId} in gamemode {stats.GameMode} acc value from {stats.Accuracy} -> {acc}");

        if (stats.MaxCombo != tempUserStats.MaxCombo)
            ChatCommandRepository.TrySendMessage(userIdToSendUpdatesTo, $"Updated user id {stats.UserId} in gamemode {stats.GameMode} max combo value from {stats.MaxCombo} -> {tempUserStats.MaxCombo}");

        if (stats.PlayCount != tempUserStats.PlayCount)
            ChatCommandRepository.TrySendMessage(userIdToSendUpdatesTo, $"Updated user id {stats.UserId} in gamemode {stats.GameMode} playcount value from {stats.PlayCount} -> {tempUserStats.PlayCount}");

        if (stats.TotalScore != tempUserStats.TotalScore)
            ChatCommandRepository.TrySendMessage(userIdToSendUpdatesTo, $"Updated user id {stats.UserId} in gamemode {stats.GameMode} total score value from {stats.TotalScore} -> {tempUserStats.TotalScore}");

        if (stats.RankedScore != tempUserStats.RankedScore)
            ChatCommandRepository.TrySendMessage(userIdToSendUpdatesTo, $"Updated user id {stats.UserId} in gamemode {stats.GameMode} ranked score value from {stats.RankedScore} -> {tempUserStats.RankedScore}");

        if (stats.TotalHits != tempUserStats.TotalHits)
            ChatCommandRepository.TrySendMessage(userIdToSendUpdatesTo, $"Updated user id {stats.UserId} in gamemode {stats.GameMode} total hits value from {stats.TotalHits} -> {tempUserStats.TotalHits}");

        stats.PlayCount = tempUserStats.PlayCount;
        stats.TotalScore = tempUserStats.TotalScore;
        stats.RankedScore = tempUserStats.RankedScore;
        stats.TotalHits = tempUserStats.TotalHits;

        stats.PerformancePoints = pp;
        stats.Accuracy = acc;

        await database.DbContext.Entry(stats).Reference(s => s.User).LoadAsync();

        var user = stats.User;

        await database.Users.Stats.UpdateUserStats(stats, user);
    }
}