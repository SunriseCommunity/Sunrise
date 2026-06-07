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
public class ScoreSideEffectsPublisherServiceTests(IntegrationDatabaseFixture fixture, bool reuseScopeInContext = true) : DatabaseTest(fixture, reuseScopeInContext)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestPublishScoreSubmissionSideEffectsWithoutBeatmapReturnsError()
    {
        // Arrange
        using var scope = Scope;
        var service = scope.ServiceProvider.GetRequiredService<ScoreSideEffectsPublisherService>();
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        var ctx = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades);

        // Act
        var result = await service.PublishScoreSubmissionSideEffects(
            BaseSession.GenerateServerSession(),
            ctx,
            CancellationToken.None);

        // Assert
        Assert.NotEmpty(result.Error);

        Assert.Equal("Beatmap and beatmap set must be present in context to publish score side effects.", result.Error);
    }

    [Fact]
    public async Task TestPublishScoreSubmissionSideEffectsWithNewFirstPlaceSendsAnnouncement()
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
        previousTopScore.EnrichWithUserData(otherUser);
        previousTopScore.Mods = Mods.None;
        previousTopScore.TotalScore = 900;
        previousTopScore.EnrichWithBeatmapData(beatmap);
        previousTopScore.LocalProperties = previousTopScore.LocalProperties.FromScore(previousTopScore);
        await CreateTestScore(previousTopScore);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score.Mods = Mods.None;
        score.TotalScore = 1000;
        score.EnrichWithBeatmapData(beatmap);
        score.LocalProperties = score.LocalProperties.FromScore(score);
        score = await CreateTestScore(score);

        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        ApplyScoreToUserStats(userStats, score);

        var ctx = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades, beatmap, beatmapSet);

        // Act
        var result = await service.PublishScoreSubmissionSideEffects(
            BaseSession.GenerateServerSession(),
            ctx,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var chatPacket = GetSessionPackets(session).FirstOrDefault(packet => packet.Type == PacketType.ServerChatMessage);
        Assert.NotNull(chatPacket);

        var chatMessage = new BanchoChatMessage(chatPacket.Data);
        Assert.Equal(Configuration.BotUsername, chatMessage.Sender);
        Assert.Equal("#announce", chatMessage.Channel);
        Assert.Equal(ScoreSubmissionUtil.GetNewFirstPlaceString(score, beatmapSet, beatmap), chatMessage.Message);
    }

    [Fact]
    public async Task TestPublishScoreSubmissionSideEffectsWithoutLeaderboardTakeoverDoesNotSendAnnouncement()
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
        existingBest.EnrichWithUserData(user);
        existingBest.Mods = Mods.None;
        existingBest.TotalScore = 900;
        existingBest.EnrichWithBeatmapData(beatmap);
        existingBest.LocalProperties = existingBest.LocalProperties.FromScore(existingBest);
        await CreateTestScore(existingBest);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score.Mods = Mods.None;
        score.TotalScore = 1000;
        score.EnrichWithBeatmapData(beatmap);
        score.LocalProperties = score.LocalProperties.FromScore(score);
        score = await CreateTestScore(score);

        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        ApplyScoreToUserStats(userStats, score);

        var ctx = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades, beatmap, beatmapSet);

        // Act
        var result = await service.PublishScoreSubmissionSideEffects(
            BaseSession.GenerateServerSession(),
            ctx,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        Assert.DoesNotContain(GetSessionPackets(session), packet => packet.Type == PacketType.ServerChatMessage);
    }

    [Fact]
    public async Task TestPublishScoreSubmissionSideEffectsWithRelaxFirstPlaceUsesScoreValueComparison()
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

        var otherBeatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        otherBeatmapSet.IgnoreBeatmapRanking();
        var otherBeatmap = otherBeatmapSet.Beatmaps!.First();

        var overallBest = _mocker.Score.GetBestScoreableRandomScore();
        overallBest.EnrichWithUserData(user);
        overallBest.GameMode = GameMode.Standard;
        overallBest.Mods = Mods.Relax;
        overallBest.TotalScore = 1000;
        overallBest.PerformancePoints = 150;
        overallBest.EnrichWithBeatmapData(otherBeatmap);
        overallBest.LocalProperties = overallBest.LocalProperties.FromScore(overallBest);
        await CreateTestScore(overallBest);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var secondPlace = _mocker.Score.GetBestScoreableRandomScore();
        secondPlace.EnrichWithUserData(otherUser);
        secondPlace.GameMode = GameMode.Standard;
        secondPlace.Mods = Mods.Relax;
        secondPlace.TotalScore = 5000;
        secondPlace.PerformancePoints = 140;
        secondPlace.EnrichWithBeatmapData(beatmap);
        secondPlace.LocalProperties = secondPlace.LocalProperties.FromScore(secondPlace);
        await CreateTestScore(secondPlace);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score.GameMode = GameMode.Standard;
        score.Mods = Mods.Relax;
        score.TotalScore = 1200;
        score.PerformancePoints = 160;
        score.EnrichWithBeatmapData(beatmap);
        score.LocalProperties = score.LocalProperties.FromScore(score);
        score = await CreateTestScore(score);

        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        ApplyScoreToUserStats(userStats, score);

        var ctx = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades, beatmap, beatmapSet);

        // Act
        var result = await service.PublishScoreSubmissionSideEffects(
            BaseSession.GenerateServerSession(),
            ctx,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

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