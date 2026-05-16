using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Services;
using Sunrise.Processing.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils.Processing;
using Xunit;
using Mods = osu.Shared.Mods;

namespace Sunrise.Processing.Tests.Services;

[Collection("Integration tests collection")]
public class ScoreSideEffectsPublisherServiceTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestPublishScoreSideEffectsAndBuildSubmissionResponseWithoutBeatmapThrows()
    {
        // Arrange
        using var scope = Scope;
        var service = scope.ServiceProvider.GetRequiredService<ScoreSideEffectsPublisherService>();
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.User = user;
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        var ctx = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PublishScoreSideEffectsAndBuildSubmissionResponse(
                BaseSession.GenerateServerSession(),
                ctx,
                userStats.Clone(),
                CancellationToken.None));

        // Assert
        Assert.Equal("Cannot publish side effects without beatmap and beatmap set on context.", exception.Message);
    }

    [Fact]
    public async Task TestPublishScoreSideEffectsAndBuildSubmissionResponseWithNewFirstPlaceSendsAnnouncement()
    {
        // Arrange
        using var scope = Scope;
        var service = scope.ServiceProvider.GetRequiredService<ScoreSideEffectsPublisherService>();
        var channels = scope.ServiceProvider.GetRequiredService<ChatChannelRepository>();

        var user = await CreateTestUser();
        var session = CreateTestSession(user);
        channels.JoinChannel("#announce", session);
        session.GetContent();

        var otherUser = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var previousTopScore = _mocker.Score.GetBestScoreableRandomScore();
        previousTopScore.UserId = otherUser.Id;
        previousTopScore.Mods = Mods.None;
        previousTopScore.TotalScore = 900;
        previousTopScore.EnrichWithBeatmapData(beatmap);
        previousTopScore.LocalProperties = previousTopScore.LocalProperties.FromScore(previousTopScore);
        await CreateTestScore(previousTopScore);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.Mods = Mods.None;
        score.TotalScore = 1000;
        score.EnrichWithBeatmapData(beatmap);
        score.LocalProperties = score.LocalProperties.FromScore(score);
        score = await CreateTestScore(score);
        score.User = user;

        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        var prevUserStats = userStats.Clone();
        ApplyScoreToUserStats(userStats, score);

        var ctx = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades, beatmap, beatmapSet);

        // Act
        var response = await service.PublishScoreSideEffectsAndBuildSubmissionResponse(
            BaseSession.GenerateServerSession(),
            ctx,
            prevUserStats,
            CancellationToken.None);

        // Assert
        Assert.NotEmpty(response);

        var chatPacket = GetSessionPackets(session).FirstOrDefault(packet => packet.Type == PacketType.ServerChatMessage);
        Assert.NotNull(chatPacket);

        var chatMessage = new BanchoChatMessage(chatPacket.Data);
        Assert.Equal(Configuration.BotUsername, chatMessage.Sender);
        Assert.Equal("#announce", chatMessage.Channel);
        Assert.Equal(ScoreSubmissionUtil.GetNewFirstPlaceString(score, beatmapSet, beatmap), chatMessage.Message);
    }

    [Fact]
    public async Task TestPublishScoreSideEffectsAndBuildSubmissionResponseWithoutLeaderboardTakeoverDoesNotSendAnnouncement()
    {
        // Arrange
        using var scope = Scope;
        var service = scope.ServiceProvider.GetRequiredService<ScoreSideEffectsPublisherService>();
        var channels = scope.ServiceProvider.GetRequiredService<ChatChannelRepository>();

        var user = await CreateTestUser();
        var session = CreateTestSession(user);
        channels.JoinChannel("#announce", session);
        session.GetContent();

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var existingBest = _mocker.Score.GetBestScoreableRandomScore();
        existingBest.UserId = user.Id;
        existingBest.Mods = Mods.None;
        existingBest.TotalScore = 900;
        existingBest.EnrichWithBeatmapData(beatmap);
        existingBest.LocalProperties = existingBest.LocalProperties.FromScore(existingBest);
        await CreateTestScore(existingBest);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.Mods = Mods.None;
        score.TotalScore = 1000;
        score.EnrichWithBeatmapData(beatmap);
        score.LocalProperties = score.LocalProperties.FromScore(score);
        score = await CreateTestScore(score);
        score.User = user;

        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        var prevUserStats = userStats.Clone();
        ApplyScoreToUserStats(userStats, score);

        var ctx = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades, beatmap, beatmapSet);

        // Act
        _ = await service.PublishScoreSideEffectsAndBuildSubmissionResponse(
            BaseSession.GenerateServerSession(),
            ctx,
            prevUserStats,
            CancellationToken.None);

        // Assert
        Assert.DoesNotContain(GetSessionPackets(session), packet => packet.Type == PacketType.ServerChatMessage);
    }

    private async Task<(UserStats UserStats, UserGrades UserGrades)> LoadUserState(User user, GameMode mode)
    {
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, mode);
        var userGrades = await Database.Users.Grades.GetUserGrades(user.Id, mode);

        Assert.NotNull(userStats);
        Assert.NotNull(userGrades);

        return (userStats, userGrades);
    }

    private static void ApplyScoreToUserStats(UserStats userStats, Score score)
    {
        userStats.PlayCount = 1;
        userStats.PlayTime = score.TimeElapsed;
        userStats.TotalScore = score.TotalScore;
        userStats.RankedScore = score.TotalScore;
        userStats.MaxCombo = score.MaxCombo;
        userStats.Accuracy = score.Accuracy;
        userStats.PerformancePoints = score.PerformancePoints;
        userStats.TotalHits = score.Count300 + score.Count100 + score.Count50 + score.CountMiss + score.CountKatu + score.CountGeki;
    }

    private static List<BanchoPacket> GetSessionPackets(Session session)
    {
        var content = session.GetContent();
        using var buffer = new MemoryStream(content);
        return BanchoSerializer.DeserializePackets(buffer).ToList();
    }
}