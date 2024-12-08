using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Managers;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Chat.Commands;

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
                CommandRepository.SendMessage(session, "User not found.");
                return;
            }

            userId = user.Id;
        }

        var scores = await database.ScoreService.GetUserScores(userId, session.Attributes.Status.PlayMode, ScoreTableType.Best);

        var bestScores = scores.OrderByDescending(x => x.PerformancePoints).Take(5).ToList();

        var result = $"[★ {(args?.Length > 0 ? args[0] : session.User.Username)}'s Best Scores]\n";

        foreach (var (score, index) in bestScores.Select((value, i) => (value, i)))
        {
            var beatmapSet = await BeatmapManager.GetBeatmapSet(session, beatmapHash: score.BeatmapHash);

            if (beatmapSet == null) continue;

            var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == score.BeatmapId);

            // Mods can change difficulty rating, important to recalculate it for right medal unlocking
            if ((int)score.GameMode != beatmap.ModeInt || (int)score.Mods > 0)
                beatmap.DifficultyRating = await Calculators
                    .RecalcuteBeatmapDifficulty(session, score.BeatmapId, (int)score.GameMode, score.Mods);

            result +=
                $"[{index + 1}] [{beatmap!.Url.Replace("ppy.sh", Configuration.Domain)} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap?.Version}]] {score.Mods.GetModsString()}| GameMode: {score.GameMode} | Acc: {score.Accuracy:0.00}% | {score.PerformancePoints:0.00}pp | {Parsers.SecondsToString(beatmap?.TotalLength ?? 0)} | {beatmap?.DifficultyRating:0.00} ★\n";
        }

        CommandRepository.SendMessage(session, result);
    }
}