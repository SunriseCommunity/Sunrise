using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Services;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Xunit;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using Mods = osu.Shared.Mods;

namespace Sunrise.Processing.Tests.Services;

[Collection("Integration tests collection")]
public class MedalServiceTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestUnlockAndGetNewMedalsWithRankedPassedScoreReturnsSeededSkillMedal()
    {
        // Arrange
        var medalService = Scope.ServiceProvider.GetRequiredService<MedalService>();
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);

        Assert.NotNull(userStats);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.GameMode = GameMode.Standard;
        score.MaxCombo = 100;
        score.Perfect = false;
        score.Mods = Mods.None;

        var beatmap = _mocker.Beatmap.GetRandomBeatmap();
        beatmap.EnrichWithScoreData(score);
        beatmap.DifficultyRating = 1;
        beatmap.StatusString = "ranked";

        // Act
        var result = await medalService.UnlockAndGetNewMedals(score, beatmap, userStats);

        // Assert
        Assert.Equal("1+Rising Star+Can't go forward without the first steps.", result);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Single(userMedals);
        Assert.Equal(1, userMedals[0].MedalId);
    }

    [Fact]
    public async Task TestUnlockAndGetNewMedalsWithPassedNoFailScoreReturnsOnlyNoFailModIntroductionMedal()
    {
        // Arrange
        var medalService = Scope.ServiceProvider.GetRequiredService<MedalService>();
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);

        Assert.NotNull(userStats);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.GameMode = GameMode.Standard;
        score.Mods = Mods.NoFail;

        var beatmap = _mocker.Beatmap.GetRandomBeatmap();
        beatmap.EnrichWithScoreData(score);
        beatmap.DifficultyRating = 1;
        beatmap.StatusString = "ranked";

        // Act
        var result = await medalService.UnlockAndGetNewMedals(score, beatmap, userStats);

        // Assert
        Assert.Equal("97+Risk Averse+Safety nets are fun!", result);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Single(userMedals);
        Assert.Equal(97, userMedals[0].MedalId);
    }

    [Fact]
    public async Task TestUnlockAndGetNewMedalsWithFailedScoreReturnsEmptyString()
    {
        // Arrange
        var medalService = Scope.ServiceProvider.GetRequiredService<MedalService>();
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);

        Assert.NotNull(userStats);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.IsPassed = false;

        var beatmap = _mocker.Beatmap.GetRandomBeatmap();

        // Act
        var result = await medalService.UnlockAndGetNewMedals(score, beatmap, userStats);

        // Assert
        Assert.Equal(string.Empty, result);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Empty(userMedals);
    }

    [Fact]
    public async Task TestUnlockAndGetNewMedalsWithUnscoreableBeatmapReturnsEmptyString()
    {
        // Arrange
        var medalService = Scope.ServiceProvider.GetRequiredService<MedalService>();
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);

        Assert.NotNull(userStats);

        var score = _mocker.Score.GetBestScoreableRandomScore();

        var beatmap = _mocker.Beatmap.GetRandomBeatmap();
        beatmap.StatusString = "pending";

        // Act
        var result = await medalService.UnlockAndGetNewMedals(score, beatmap, userStats);

        // Assert
        Assert.Equal(string.Empty, result);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Empty(userMedals);
    }

    [Fact]
    public async Task TestUnlockAndGetNewMedalsWithPreviouslyUnlockedMedalReturnsEmptyString()
    {
        // Arrange
        var medalService = Scope.ServiceProvider.GetRequiredService<MedalService>();
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);

        Assert.NotNull(userStats);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.GameMode = GameMode.Standard;
        score.MaxCombo = 100;
        score.Perfect = false;
        score.Mods = Mods.None;

        var beatmap = _mocker.Beatmap.GetRandomBeatmap();
        beatmap.EnrichWithScoreData(score);
        beatmap.DifficultyRating = 1;
        beatmap.StatusString = "ranked";

        await medalService.UnlockAndGetNewMedals(score, beatmap, userStats);

        // Act
        var result = await medalService.UnlockAndGetNewMedals(score, beatmap, userStats);

        // Assert
        Assert.Equal(string.Empty, result);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Single(userMedals);
        Assert.Equal(1, userMedals[0].MedalId);
    }
}