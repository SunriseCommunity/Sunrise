using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("recalculatescores", requiredPrivileges: UserPrivileges.Developer)]
public class RecalculateScoresCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            CommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}recalculatescores <modeEnum>; Example: {Configuration.BotPrefix}recalculatescores 0 for osu std.");
            return Task.CompletedTask;
        }

        if (!Enum.TryParse(args[0], out GameMode mode))
        {
            CommandRepository.SendMessage(session, "Invalid mode.");
            return Task.CompletedTask;
        }

        if (Configuration.OnMaintenance)
        {
            CommandRepository.SendMessage(session, "Server is in maintenance mode. Recalculation is not possible.");
            return Task.CompletedTask;
        }

        CommandRepository.SendMessage(session,
            "Recalculation started. Server will enter maintenance mode until it's done.");

        Configuration.OnMaintenance = true;

        _ = RecalculateScores(session, mode);
        return Task.CompletedTask;
    }

    private async Task RecalculateScores(Session session, GameMode mode)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var userSession in sessions.GetSessions())
        {
            userSession.SendBanchoMaintenance();
        }

        var startTime = DateTime.UtcNow;

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var allScores = await database.ScoreService.GetAllScores(mode);
        var scoresReviewedTotal = 0;

        foreach (var score in allScores)
        {
            var oldPerformancePoints = score.PerformancePoints;
            score.PerformancePoints = Calculators.CalculatePerformancePoints(session, score);
            await database.ScoreService.UpdateScore(score);

            scoresReviewedTotal++;
            CommandRepository.SendMessage(session, $"Updated score {score.Id} from {oldPerformancePoints} to {score.PerformancePoints}");
            CommandRepository.SendMessage(session, $"Total scores reviewed: {scoresReviewedTotal}");
        }

        CommandRepository.SendMessage(session,
            $"Updating scores for mode {mode} is finished. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");

        Configuration.OnMaintenance = false;

        CommandRepository.SendMessage(session, "Recalculation finished. Server is back online.");
    }
}