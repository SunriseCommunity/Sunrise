using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Extensions.Scores;

namespace Sunrise.Tests.Utils.Processing;

public static class ScoreProcessingTestDataFactory
{
    public static ScoreProcessingQueue CreateQueueEntry(
        Score score,
        string username = "player",
        string clientHash = "client-hash",
        int? replayFileId = 321,
        string? storyboardHash = null)
    {
        score.ScoreHash = score.ComputeOnlineHash(username, clientHash, storyboardHash);

        return new ScoreProcessingQueue
        {
            UserId = score.UserId,
            ScoreHash = score.ScoreHash,
            ScoreSerialized = score.ToScoreString(username),
            BeatmapHash = score.BeatmapHash,
            TimeElapsed = score.TimeElapsed,
            OsuVersion = score.OsuVersion,
            ClientHash = clientHash,
            ReplayFileId = replayFileId,
            StoryboardHash = storyboardHash,
            UserHash = clientHash,
            WhenPlayed = score.WhenPlayed
        };
    }
}