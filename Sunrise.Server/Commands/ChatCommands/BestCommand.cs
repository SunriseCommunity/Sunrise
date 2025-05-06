using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Commands.ChatCommands;

[ChatCommand("best")]
public class BestCommand : IChatCommand
{
    // TODO: Should support choosing game mode as an argument

    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        var userId = session.UserId;

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var providedUsername = args?.Length > 0 ? string.Join(" ", args[..]) : null;
        var user = await database.Users.GetUser(username: providedUsername, id: providedUsername != null ? null : userId);

        if (user == null)
        {
            ChatCommandRepository.SendMessage(session, "User not found.");
            return;
        }

        var (bestScores, _) = await database.Scores.GetUserScores(user.Id,
            (GameMode)session.Attributes.Status.PlayMode,
            ScoreTableType.Best,
            new QueryOptions(true, new Pagination(1, 5))
            {
                IgnoreCountQueryIfExists = true
            });

        var result = $"[★ {user.Username}'s Best Scores]\n";

        var beatmapService = scope.ServiceProvider.GetRequiredService<BeatmapService>();

        foreach (var (score, index) in bestScores.Select((value, i) => (value, i)))
        {
            var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapHash: score.BeatmapHash);

            if (beatmapSetResult.IsFailure)
            {
                ChatCommandRepository.SendMessage(session, beatmapSetResult.Error.Message);
                return;
            }

            var beatmapSet = beatmapSetResult.Value;

            if (beatmapSet == null)
            {
                ChatCommandRepository.SendMessage(session, "BeatmapSet not found.");
                continue;
            }

            var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == score.BeatmapId);

            if (beatmap == null)
            {
                ChatCommandRepository.SendMessage(session, "Beatmap not found.");
                continue;
            }

            result += $"[{index + 1}] {await score.GetBeatmapInGameChatString(beatmapSet, session)}\n";
        }

        ChatCommandRepository.SendMessage(session, result);
    }
}