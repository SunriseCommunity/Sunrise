using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils.Calculators;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("recalculatescores", requiredPrivileges: UserPrivilege.Developer)]
public class RecalculateScoresCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 2)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}recalculatescores <modeEnum> <startFromId>; Example: {Configuration.BotPrefix}recalculatescores 0 10 for osu std.");
            return Task.CompletedTask;
        }

        if (!Enum.TryParse(args[0], out GameMode mode))
        {
            ChatCommandRepository.SendMessage(session, "Invalid mode.");
            return Task.CompletedTask;
        }

        if (!int.TryParse(args[1], out var startFromId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid startFromId.");
            return Task.CompletedTask;
        }

        if (Configuration.OnMaintenance)
        {
            ChatCommandRepository.SendMessage(session, "Server is in maintenance mode. Recalculation is not possible.");
            return Task.CompletedTask;
        }

        ChatCommandRepository.SendMessage(session,
            "Recalculation started. Server will enter maintenance mode until it's done.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => RecalculateScores(session.UserId, mode, startFromId));

        return Task.CompletedTask;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task RecalculateScores(int userId, GameMode mode, int startFromId)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var userSession in sessions.GetSessions())
        {
            userSession.SendBanchoMaintenance();
        }

        var startTime = DateTime.UtcNow;

        var pageSize = 100;
        var scoresReviewedTotal = 0;

        for (var x = 1;; x++)
        {
            using var scope = ServicesProviderHolder.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
            var calculatorService = scope.ServiceProvider.GetRequiredService<CalculatorService>();

            var user = await database.Users.GetUser(userId);
            if (user == null)
                return;

            var session = BaseSession.GenerateServerSession();

            var allScores = await database.Scores.GetScores(mode, new QueryOptions(new Pagination(x, pageSize)), startFromId);

            foreach (var score in allScores)
            {
                var oldPerformancePoints = score.PerformancePoints;
                var oldAccuracy = score.Accuracy;

                score.Accuracy = PerformanceCalculator.CalculateAccuracy(score);

                var retryCount = 0;

                while (retryCount < 10)
                {
                    var scorePerformanceResult = await calculatorService.CalculateScorePerformance(session, score);

                    if (scorePerformanceResult.IsSuccess)
                    {
                        score.PerformancePoints = scorePerformanceResult.Value.PerformancePoints;
                        await database.Scores.UpdateScore(score);

                        scoresReviewedTotal++;

                        ChatCommandRepository.TrySendMessage(userId, $"Updated score {score.Id} acc from {oldAccuracy} to {score.Accuracy}");
                        ChatCommandRepository.TrySendMessage(userId, $"Updated score {score.Id} pp from {oldPerformancePoints} to {score.PerformancePoints}");
                        ChatCommandRepository.TrySendMessage(userId, $"Total scores reviewed: {scoresReviewedTotal}");

                        break;
                    }

                    retryCount++;

                    if (retryCount >= 10)
                    {
                        ChatCommandRepository.TrySendMessage(userId, $"Failed to update {score.Id} after 10 retries: {scorePerformanceResult.Error}");
                        ChatCommandRepository.TrySendMessage(userId, "Stopping the recalculation process... Please try again later.");
                        Configuration.OnMaintenance = false;
                        ChatCommandRepository.TrySendMessage(userId, "Recalculation is paused. Server is back online.");
                        throw new Exception($"Failed to update {score.Id} after 10 retries: {scorePerformanceResult.Error}");
                    }

                    ChatCommandRepository.TrySendMessage(userId, $"Retrying update for score {score.Id} (Attempt {retryCount}/10)...");
                    await Task.Delay(10_000);
                }
            }

            if (allScores.Count < pageSize) break;
        }

        ChatCommandRepository.TrySendMessage(userId,
            $"Updating scores for mode {mode} is finished. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");

        Configuration.OnMaintenance = false;

        ChatCommandRepository.TrySendMessage(userId, "Recalculation finished. Server is back online.");
    }
}