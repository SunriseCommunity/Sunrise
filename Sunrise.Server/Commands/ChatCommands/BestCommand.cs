using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils.Converters;
using Sunrise.Shared.Utils.Performance;

namespace Sunrise.Server.Commands.ChatCommands;

[ChatCommand("best")]
public class BestCommand : IChatCommand
{
    // TODO: Should support choosing game mode as an argument

    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        var userId = session.User.Id;

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        if (args is { Length: >= 1 })
        {
            var user = await database.UserService.GetUser(username: args[0]);

            if (user == null)
            {
                ChatCommandRepository.SendMessage(session, "User not found.");
                return;
            }

            userId = user.Id;
        }

        var scores = await database.ScoreService.GetUserScores(userId, (GameMode)session.Attributes.Status.PlayMode, ScoreTableType.Best);

        var bestScores = scores.OrderByDescending(x => x.PerformancePoints).Take(5).ToList();

        var result = $"[★ {(args?.Length > 0 ? args[0] : session.User.Username)}'s Best Scores]\n";

        foreach (var (score, index) in bestScores.Select((value, i) => (value, i)))
        {
            var beatmapSet = await BeatmapRepository.GetBeatmapSet(session, beatmapHash: score.BeatmapHash);

            if (beatmapSet == null) continue;

            var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == score.BeatmapId);

            // Mods can change difficulty rating, important to recalculate it for right medal unlocking
            if ((int)score.GameMode != beatmap.ModeInt || (int)score.Mods > 0)
                beatmap.DifficultyRating = await Calculators
                    .RecalcuteBeatmapDifficulty(session, score.BeatmapId, (int)score.GameMode, score.Mods);

            result +=
                $"[{index + 1}] [{beatmap!.Url.Replace("ppy.sh", Configuration.Domain)} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap?.Version}]] {score.Mods.GetModsString()}| GameMode: {score.GameMode} | Acc: {score.Accuracy:0.00}% | {score.PerformancePoints:0.00}pp | {TimeConverter.SecondsToString(beatmap?.TotalLength ?? 0)} | {beatmap?.DifficultyRating:0.00} ★\n";
        }

        ChatCommandRepository.SendMessage(session, result);
    }
}