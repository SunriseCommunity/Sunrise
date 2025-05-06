using System.Net;
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
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils.Calculators;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("recalculatescores", requiredPrivileges: UserPrivilege.Developer)]
public class RecalculateScoresCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 3)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}recalculatescores <modeEnum> <startFromId> <isStartMaintenance>; Example: {Configuration.BotPrefix}recalculatescores 0 10 for osu std starting from score 10 with maintenance mode on..");
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

        if (!bool.TryParse(args[2], out var isStartMaintenance))
        {
            ChatCommandRepository.SendMessage(session, "Invalid isStartMaintenance value.");
            return Task.CompletedTask;
        }

        BackgroundTaskService.TryStartNewBackgroundJob<RecalculateScoresCommand>(
            () => RecalculateScores(session.UserId, CancellationToken.None, mode, startFromId),
            message => ChatCommandRepository.TrySendMessage(session.UserId, message),
            isStartMaintenance);

        return Task.CompletedTask;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task RecalculateScores(int userId, CancellationToken ct, GameMode mode, int startFromId)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<RecalculateScoresCommand>(
            async () =>
            {

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

                    var (pageScores, _) = await database.Scores.GetScores(mode,
                        new QueryOptions(new Pagination(x, pageSize))
                        {
                            IgnoreCountQueryIfExists = true
                        },
                        startFromId);

                    foreach (var score in pageScores)
                    {
                        var oldPerformancePoints = score.PerformancePoints;
                        var oldAccuracy = score.Accuracy;

                        score.Accuracy = PerformanceCalculator.CalculateAccuracy(score);

                        var retryCount = 0;

                        while (retryCount < 10)
                        {
                            var scorePerformanceResult = await calculatorService.CalculateScorePerformance(session, score);

                            if (scorePerformanceResult.IsFailure)
                            {
                                if (scorePerformanceResult.Error.Status == HttpStatusCode.NotFound)
                                {
                                    var result = await database.Scores.MarkScoreAsDeleted(score);

                                    if (result.IsFailure)
                                    {
                                        ChatCommandRepository.TrySendMessage(userId, $"Failed to update score {score.Id} as DELETED, error: {result.Error}");
                                        throw new Exception($"Failed to update score {score.Id}, error: {result.Error} ");
                                    }

                                    ChatCommandRepository.TrySendMessage(userId, $"Updated score id {score.Id} in gamemode {mode} to be marked as DELETED, since we couldn't find beatmap it was played on");
                                    break;
                                }
                            }

                            if (scorePerformanceResult.IsSuccess)
                            {
                                score.PerformancePoints = scorePerformanceResult.Value.PerformancePoints;

                                ct.ThrowIfCancellationRequested();

                                var result = await database.Scores.UpdateScore(score);

                                if (result.IsFailure)
                                {
                                    ChatCommandRepository.TrySendMessage(userId, $"Failed to update score {score.Id}, error: {result.Error}");
                                    throw new Exception($"Failed to update score {score.Id}, error: {result.Error} ");
                                }

                                scoresReviewedTotal++;

                                if (scoresReviewedTotal % 100 == 0)
                                {
                                    ChatCommandRepository.TrySendMessage(userId, $"Scores reviewed in total: {scoresReviewedTotal}");
                                }

                                const float tolerance = 0.0001f;

                                if (Math.Abs(oldAccuracy - score.Accuracy) > tolerance)
                                    ChatCommandRepository.TrySendMessage(userId, $"Updated score id {score.Id} in gamemode {mode} acc value from {oldAccuracy} to {score.Accuracy}");

                                if (Math.Abs(oldPerformancePoints - score.PerformancePoints) > tolerance)
                                    ChatCommandRepository.TrySendMessage(userId, $"Updated score id {score.Id} in gamemode {mode} pp value from {oldPerformancePoints} to {score.PerformancePoints}");

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
                            await Task.Delay(10_000, ct);
                        }
                    }

                    if (pageScores.Count < pageSize) break;
                }
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}