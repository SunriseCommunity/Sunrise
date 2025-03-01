using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils.Converters;

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

        var (scores, _) = await database.Scores.GetUserScores(userId, (GameMode)session.Attributes.Status.PlayMode, ScoreTableType.Best); // TODO: Should be optimised!

        var bestScores = scores.OrderByDescending(x => x.PerformancePoints).Take(5).ToList();

        var result = $"[★ {user.Username}'s Best Scores]\n";

        var beatmapService = scope.ServiceProvider.GetRequiredService<BeatmapService>();
        var calculatorService = scope.ServiceProvider.GetRequiredService<CalculatorService>();

        foreach (var (score, index) in bestScores.Select((value, i) => (value, i)))
        {
            var beatmapSet = await beatmapService.GetBeatmapSet(session, beatmapHash: score.BeatmapHash);

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

            // Mods can change difficulty rating, important to recalculate it for right medal unlocking
            if ((int)score.GameMode != beatmap.ModeInt || (int)score.Mods > 0)
                beatmap.DifficultyRating = await calculatorService
                    .RecalculateBeatmapDifficulty(session, score.BeatmapId, (int)score.GameMode, score.Mods);

            result +=
                $"[{index + 1}] [{beatmap!.Url.Replace("ppy.sh", Configuration.Domain)} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap?.Version}]] {score.Mods.GetModsString()}| GameMode: {score.GameMode} | Acc: {score.Accuracy:0.00}% | {score.PerformancePoints:0.00}pp | {TimeConverter.SecondsToString(beatmap?.TotalLength ?? 0)} | {beatmap?.DifficultyRating:0.00} ★\n";
        }

        ChatCommandRepository.SendMessage(session, result);
    }
}