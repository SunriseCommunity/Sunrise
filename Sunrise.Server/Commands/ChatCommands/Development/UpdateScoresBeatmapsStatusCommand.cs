using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("updatescoresbeatmapstatus", requiredPrivileges: UserPrivilege.Developer)]
public class UpdateScoresBeatmapsStatusCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            ChatCommandRepository.SendMessage(session, "Server is in maintenance mode. Starting recalculation is not possible.");
            return Task.CompletedTask;
        }

        ChatCommandRepository.SendMessage(session,
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
            var beatmap = await BeatmapRepository.GetBeatmapSet(session, beatmapId: group.Key);

            var status = BeatmapStatus.NotSubmitted;

            if (beatmap == null)
            {
                ChatCommandRepository.TrySendMessage(userId, $"Beatmap {group.Key} not found. Setting status to graveyard.");
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

            ChatCommandRepository.TrySendMessage(userId, $"Updated {group.Count()} scores for beatmap {group.Key} to status {status}");
            ChatCommandRepository.TrySendMessage(userId, $"Total scores reviewed: {scoresReviewedTotal}");

            // Prevent rate limiting on beatmap mirrors
            await Task.Delay(2000);
        }

        ChatCommandRepository.TrySendMessage(userId,
            $"Updating beatmap status on scores is finished. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");

        Configuration.OnMaintenance = false;

        ChatCommandRepository.TrySendMessage(userId, "Recalculation finished. Server is back online.");
    }
}