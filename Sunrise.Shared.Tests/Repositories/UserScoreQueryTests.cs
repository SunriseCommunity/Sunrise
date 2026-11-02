using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Mods = osu.Shared.Mods;

namespace Sunrise.Shared.Tests.Repositories;

[Collection("Integration tests collection")]
public class UserScoreQueryTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture, true)
{
    private readonly MockService _mocker = new();

    [Theory]
    [InlineData(false, GameMode.Standard)]
    [InlineData(true, GameMode.Standard)]
    [InlineData(false, GameMode.Taiko)]
    [InlineData(true, GameMode.Taiko)]
    public async Task PersonalBestsStayPerUserWhileTopStillComparesAllUsers(bool rankByPp, GameMode mode)
    {
        EnvManager.Set("General:UseNewPerformanceCalculationAlgorithm", rankByPp.ToString());
        var user = await CreateTestUser();
        var other = await CreateTestUser();
        var first = _mocker.Score.GetBestScoreableRandomScore();
        first.EnrichWithUserData(user);
        first.GameMode = mode;
        first.Mods = Mods.None;
        first.SubmissionStatus = SubmissionStatus.Best;
        first.IsPassed = true;
        first.SetBeatmapStatus(BeatmapStatus.Ranked);
        first.TotalScore = 200;
        first.PerformancePoints = 50;
        await CreateTestScore(first);

        var second = _mocker.Score.GetBestScoreableRandomScore();
        second.EnrichWithUserData(user);
        second.GameMode = mode;
        second.Mods = Mods.Hidden;
        second.BeatmapId = first.BeatmapId;
        second.BeatmapHash = first.BeatmapHash;
        second.SubmissionStatus = SubmissionStatus.Best;
        second.IsPassed = true;
        second.TotalScore = 100;
        second.PerformancePoints = 300;
        await CreateTestScore(second);

        var winner = _mocker.Score.GetBestScoreableRandomScore();
        winner.EnrichWithUserData(other);
        winner.GameMode = mode;
        winner.Mods = Mods.None;
        winner.BeatmapId = first.BeatmapId;
        winner.BeatmapHash = first.BeatmapHash;
        winner.SubmissionStatus = SubmissionStatus.Best;
        winner.IsPassed = true;
        winner.TotalScore = 1000;
        winner.PerformancePoints = 500;
        await CreateTestScore(winner);

        var (best, bestCount) = await Database.Scores.GetUserScores(user.Id, mode, ScoreTableType.Best, new QueryOptions(true));
        Assert.Equal(1, bestCount);
        Assert.Equal(rankByPp ? second.Id : first.Id, Assert.Single(best).Id);

        var (top, _) = await Database.Scores.GetUserScores(user.Id, mode, ScoreTableType.Top, new QueryOptions(true));
        Assert.Empty(top);
        var (otherTop, _) = await Database.Scores.GetUserScores(other.Id, mode, ScoreTableType.Top, new QueryOptions(true));
        Assert.Equal(winner.Id, Assert.Single(otherTop).Id);
    }
}
