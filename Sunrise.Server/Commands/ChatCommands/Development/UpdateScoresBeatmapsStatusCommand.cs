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

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("updatescoresbeatmapstatus", requiredPrivileges: UserPrivilege.Developer)]
public class UpdateScoresBeatmapsStatusCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        BackgroundTaskService.TryStartNewBackgroundJob<UpdateScoresBeatmapsStatusCommand>(
            () =>
                UpdateScoresBeatmapStatus(session.UserId, CancellationToken.None),
            message => ChatCommandRepository.SendMessage(session, message));

        return Task.CompletedTask;
    }

    public async Task UpdateScoresBeatmapStatus(int userId, CancellationToken ct)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<UpdateScoresBeatmapsStatusCommand>(
            async () =>
            {
                using var scope = ServicesProviderHolder.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

                var (allScores, _) = await database.Scores.GetScores(options: new QueryOptions
                    {
                        IgnoreCountQueryIfExists = true
                    },
                    ct: ct);
                var groupedScores = allScores.GroupBy(x => x.BeatmapId);

                var scoresReviewedTotal = 0;

                foreach (var group in groupedScores)
                {
                    var isNeedsUpdate = group.Any(s => s.BeatmapStatus == BeatmapStatus.Unknown);

                    scoresReviewedTotal += group.Count();

                    if (!isNeedsUpdate) continue;

                    var beatmapService = scope.ServiceProvider.GetRequiredService<BeatmapService>();

                    var session = BaseSession.GenerateServerSession();
                    var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapId: group.Key, ct: ct);

                    if (beatmapSetResult.IsFailure)
                    {
                        ChatCommandRepository.TrySendMessage(userId, $"Beatmap set {group.Key} returned error: {beatmapSetResult.Error}");
                        return;
                    }

                    var beatmapSet = beatmapSetResult.Value;

                    var status = BeatmapStatus.NotSubmitted;

                    if (beatmapSet == null)
                    {
                        ChatCommandRepository.TrySendMessage(userId, $"Beatmap set {group.Key} not found. Setting status to graveyard.");
                    }
                    else
                    {
                        status = beatmapSet.Status;
                    }

                    foreach (var score in group)
                    {
                        score.BeatmapStatus = status;
                        ct.ThrowIfCancellationRequested();
                        await database.Scores.UpdateScore(score);
                    }

                    ChatCommandRepository.TrySendMessage(userId, $"Updated {group.Count()} scores for beatmap {group.Key} to status {status}");
                    ChatCommandRepository.TrySendMessage(userId, $"Total scores reviewed: {scoresReviewedTotal}");

                    // Prevent rate limiting on beatmap mirrors
                    await Task.Delay(2000, ct);
                }
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}