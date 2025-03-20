using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("updatescoresbeatmapstatus", requiredPrivileges: UserPrivilege.Developer)]
public class UpdateScoresBeatmapsStatusCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        BackgroundTasks.TryStartNewBackgroundJob<UpdateScoresBeatmapsStatusCommand>(
            () =>
                UpdateScoresBeatmapStatus(session.UserId),
            message => ChatCommandRepository.SendMessage(session, message));

        return Task.CompletedTask;
    }

    public async Task UpdateScoresBeatmapStatus(int userId)
    {
        await BackgroundTasks.ExecuteBackgroundTask<UpdateScoresBeatmapsStatusCommand>(
            async () =>
            {
                using var scope = ServicesProviderHolder.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

                var allScores = await database.Scores.GetScores();
                var groupedScores = allScores.GroupBy(x => x.BeatmapId);

                var scoresReviewedTotal = 0;

                foreach (var group in groupedScores)
                {
                    var isNeedsUpdate = group.Any(s => s.BeatmapStatus == BeatmapStatus.Unknown);

                    scoresReviewedTotal += group.Count();

                    if (!isNeedsUpdate) continue;

                    var user = await database.Users.GetUser(userId);
                    if (user == null)
                        return;

                    var beatmapService = scope.ServiceProvider.GetRequiredService<BeatmapService>();

                    var session = new BaseSession(user);
                    var beatmap = await beatmapService.GetBeatmapSet(session, beatmapId: group.Key);

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
                        await database.Scores.UpdateScore(score);
                    }

                    ChatCommandRepository.TrySendMessage(userId, $"Updated {group.Count()} scores for beatmap {group.Key} to status {status}");
                    ChatCommandRepository.TrySendMessage(userId, $"Total scores reviewed: {scoresReviewedTotal}");

                    // Prevent rate limiting on beatmap mirrors
                    await Task.Delay(2000);
                }
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}