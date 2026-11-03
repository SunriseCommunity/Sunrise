using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using Mods = osu.Shared.Mods;

namespace Sunrise.Shared.Tests.Repositories;

[Collection("Integration tests collection")]
public class UserScoreQueryTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture, true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task ScoreCountsMatchVisibleScoresAcrossStatusesAndAfterRestriction()
    {
        var users = new[] { await CreateTestUser(), await CreateTestUser() };
        users[1].AccountStatus = UserAccountStatus.Restricted;
        await Database.DbContext.SaveChangesAsync();
        foreach (var user in users)
        foreach (var status in Enum.GetValues<SubmissionStatus>())
        foreach (var beatmapStatus in new[] { BeatmapStatus.Unknown, BeatmapStatus.Ranked, BeatmapStatus.Loved })
        {
            var score = _mocker.Score.GetBestScoreableRandomScore();
            score.EnrichWithUserData(user);
            score.GameMode = status == SubmissionStatus.Best ? GameMode.Mania : GameMode.Standard;
            score.SubmissionStatus = status;
            score.IsPassed = status != SubmissionStatus.Failed;
            score.SetBeatmapStatus(beatmapStatus);
            await CreateTestScore(score);
        }

        for (var pass = 0; pass < 2; pass++)
        {
            var expected = await Database.DbContext.Scores.FilterValidScores()
                .GroupBy(s => s.GameMode).Select(g => new { Mode = g.Key, Count = g.LongCount() })
                .ToDictionaryAsync(g => g.Mode, g => g.Count);
            Assert.Equal(expected.Values.Sum(), await Database.Scores.CountScores());
            var actual = await Database.Scores.CountScoresByGameMode();
            Assert.Equal(expected.OrderBy(x => x.Key), actual.OrderBy(x => x.Key));

            users[0].AccountStatus = UserAccountStatus.Restricted;
            await Database.DbContext.SaveChangesAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BestScoreCountsDoNotMaterializeScoreRecords(bool rankByPp)
    {
        EnvManager.Set("General:UseNewPerformanceCalculationAlgorithm", rankByPp.ToString());
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score.GameMode = GameMode.Standard;
        score.SetBeatmapStatus(BeatmapStatus.Ranked);
        await CreateTestScore(score);

        var submitted = _mocker.Score.GetBestScoreableRandomScore();
        submitted.EnrichWithUserData(user);
        submitted.GameMode = GameMode.Standard;
        submitted.SubmissionStatus = SubmissionStatus.Submitted;
        submitted.SetBeatmapStatus(BeatmapStatus.Ranked);
        await CreateTestScore(submitted);

        using var sql = new SqlCommandCapture();
        var (_, globalCount) = await Database.Scores.GetBestScoresByGameMode(GameMode.Standard);
        var (_, userCount) = await Database.Scores.GetUserScores(user.Id, GameMode.Standard, ScoreTableType.Best);
        var (_, beatmapCount) = await Database.Scores.GetBeatmapScores(score.BeatmapHash, GameMode.Standard);

        Assert.Equal(rankByPp ? 2 : 1, globalCount);
        Assert.Equal(rankByPp ? 2 : 1, userCount);
        Assert.Equal(1, beatmapCount);
        var counts = sql.Commands.Where(command => command.StartsWith("SELECT COUNT(*)", StringComparison.Ordinal));
        Assert.Equal(3, counts.Count());
        Assert.All(counts, command => Assert.DoesNotContain("`Accuracy`", command));
        Assert.All(counts, command => Assert.DoesNotContain("ORDER BY", command));
    }

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
