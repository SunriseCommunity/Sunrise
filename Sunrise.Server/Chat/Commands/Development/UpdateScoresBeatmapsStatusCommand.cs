using Hangfire;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Managers;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("updatescoresbeatmapstatus", requiredPrivileges: UserPrivileges.Developer)]
public class UpdateScoresBeatmapsStatusCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            CommandRepository.SendMessage(session, "Server is in maintenance mode. Starting recalculation is not possible.");
            return Task.CompletedTask;
        }

        CommandRepository.SendMessage(session,
            "Updating beatmap status on scores is started. Server will enter maintenance mode until it's done.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => UpdateScoresBeatmapStatus(session.User.Id));

        return Task.CompletedTask;
    }

    public async Task UpdateScoresBeatmapStatus(int userId)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var userSession in sessions.GetSessions())
        {
            userSession.SendBanchoMaintenance();
        }

        var startTime = DateTime.UtcNow;

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var allScores = await database.ScoreService.GetAllScores();
        var groupedScores = allScores.GroupBy(x => x.BeatmapId);

        var scoresReviewedTotal = 0;

        foreach (var group in groupedScores)
        {
            var isNeedsUpdate = group.Any(s => s.BeatmapStatus == BeatmapStatus.Unknown);

            scoresReviewedTotal += group.Count();

            if (!isNeedsUpdate) continue;

            var user = await database.UserService.GetUser(userId);
            if (user == null)
                return;

            var session = new BaseSession(user);
            var beatmap = await BeatmapManager.GetBeatmapSet(session, beatmapId: group.Key);

            var status = BeatmapStatus.NotSubmitted;

            if (beatmap == null)
            {
                CommandRepository.TrySendMessage(userId, $"Beatmap {group.Key} not found. Setting status to graveyard.");
            }
            else
            {
                status = beatmap.Status;
            }

            foreach (var score in group)
            {
                score.BeatmapStatus = status;
                await database.ScoreService.UpdateScore(score);
            }

            CommandRepository.TrySendMessage(userId, $"Updated {group.Count()} scores for beatmap {group.Key} to status {status}");
            CommandRepository.TrySendMessage(userId, $"Total scores reviewed: {scoresReviewedTotal}");

            // Prevent rate limiting on beatmap mirrors
            await Task.Delay(2000);
        }

        CommandRepository.TrySendMessage(userId,
            $"Updating beatmap status on scores is finished. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");

        Configuration.OnMaintenance = false;

        CommandRepository.TrySendMessage(userId, "Recalculation finished. Server is back online.");
    }
}