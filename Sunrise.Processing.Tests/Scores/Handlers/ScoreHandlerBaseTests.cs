using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Xunit;

namespace Sunrise.Processing.Tests.Scores.Handlers;

[Collection("Integration tests collection")]
public class ScoreHandlerBaseTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestLoadUserStateWithExistingUserReturnsUserStatsAndGrades()
    {
        // Arrange
        var handler = CreateHandler();
        var user = await CreateTestUser();
        var score = await CreateTestScore(user, false);

        // Act
        var result = await handler.InvokeLoadUserState(score, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.Equal(user.Id, result.Value.UserStats.UserId);
        Assert.Equal(user.Id, result.Value.UserGrades.UserId);
        Assert.True(result.Value.UserStats.LocalProperties.Rank > 0);
    }

    [Fact]
    public async Task TestLoadUserStateWithMissingUserReturnsUserNotFound()
    {
        // Arrange
        var handler = CreateHandler();
        var score = _mocker.Score.GetRandomScore();

        // Act
        var result = await handler.InvokeLoadUserState(score, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.UserNotFound, result.Error.Code);
    }

    [Fact]
    public async Task TestResolveBeatmapWithCachedBeatmapReturnsMatchingBeatmap()
    {
        // Arrange
        var handler = CreateHandler();
        var beatmapService = Scope.ServiceProvider.GetRequiredService<BeatmapService>();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);


        // Act
        var result = await handler.InvokeResolveBeatmap(beatmapService, BaseSession.GenerateServerSession(), beatmap.Checksum, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(beatmapSet.Id, result.Value.BeatmapSet.Id);
        Assert.Equal(beatmap.Checksum, result.Value.Beatmap.Checksum);
    }

    [Fact]
    public async Task TestResolveBeatmapWithMissingBeatmapReturnsPermanentBeatmapNotFound()
    {
        // Arrange
        var handler = CreateHandler();
        var beatmapService = Scope.ServiceProvider.GetRequiredService<BeatmapService>();

        // Act
        var result = await handler.InvokeResolveBeatmap(beatmapService, BaseSession.GenerateServerSession(), "missing-handler-base-hash", CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.BeatmapNotFound, result.Error.Code);
        Assert.Equal(ScoreProcessingDisposition.Permanent, result.Error.Disposition);
        Assert.Contains("Failed to fetch beatmap set:", result.Error.Message);
    }

    private TestScoreHandler CreateHandler()
    {
        return new TestScoreHandler(Database, new ScoreCommitPipeline(Database, []));
    }

    private sealed class TestScoreHandler(DatabaseService database, ScoreCommitPipeline pipeline) : ScoreHandlerBase(database, pipeline)
    {
        public Task<Result<(User User, UserStats UserStats, UserGrades UserGrades), ScoreProcessingError>> InvokeLoadUserState(Score score, CancellationToken ct)
        {
            return LoadUserState(score, ct);
        }

        public Task<Result<(BeatmapSet BeatmapSet, Beatmap Beatmap), ScoreProcessingError>> InvokeResolveBeatmap(BeatmapService beatmapService, BaseSession session, string beatmapHash, CancellationToken ct)
        {
            return ResolveBeatmap(beatmapService, session, beatmapHash, ct);
        }
    }
}