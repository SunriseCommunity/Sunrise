using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Commands.ChatCommands;

[ChatCommand("rs")]
public class RecentScoreCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var lastScore = await database.Scores.GetUserLastScore(session.UserId);

        if (lastScore == null)
        {
            ChatCommandRepository.SendMessage(session, "No recent score found.");
            return;
        }

        var beatmapService = scope.ServiceProvider.GetRequiredService<BeatmapService>();

        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapHash: lastScore.BeatmapHash);

        if (beatmapSetResult.IsFailure)
        {
            ChatCommandRepository.SendMessage(session, beatmapSetResult.Error.Message);
            return;
        }

        var beatmapSet = beatmapSetResult.Value;

        if (beatmapSet == null)
        {
            ChatCommandRepository.SendMessage(session, "Beatmap not found.");
            return;
        }

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == lastScore.BeatmapId);

        if (beatmap == null)
        {
            ChatCommandRepository.SendMessage(session, "No beatmap found.");
            return;
        }

        var scoreMessage = await lastScore.GetBeatmapInGameChatString(beatmapSet, session);

        ChatCommandRepository.SendMessage(session, scoreMessage);
    }
}