using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Services;
using Sunrise.Shared.Extensions;
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
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = GameMode.Standard;
        score.EnrichWithUserData(user);
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmapSet.IgnoreBeatmapRanking();
        beatmap.EnrichWithScoreData(score);

        beatmap.DifficultyRating = 1; // Set difficulty rating to 1 to meet the medal condition.

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var medalService = Scope.ServiceProvider.GetRequiredService<MedalService>();

        // Act
        var result = await medalService.UnlockAndGetNewMedals(score, beatmap, userStats!);

        // Assert
        Assert.Contains("1+Rising Star+Can't go forward without the first steps.", result);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Contains(userMedals, m => m.MedalId == 1);
    }

    [Fact]
    public async Task TestUnlockAndGetNewMedalsWithRankedPassedNoFailScoreReturnsNoFailModIntroductionMedal()
    {
        // Arrange
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = GameMode.Standard;
        score.EnrichWithUserData(user);

        score.Mods = Mods.NoFail;

        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var medalService = Scope.ServiceProvider.GetRequiredService<MedalService>();

        // Act
        var result = await medalService.UnlockAndGetNewMedals(score, beatmap, userStats!);

        // Assert
        Assert.Contains("97+Risk Averse+Safety nets are fun!", result);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Contains(userMedals, m => m.MedalId == 97);
    }

    [Fact]
    public async Task TestUnlockAndGetNewMedalsWithRankedPassedScoreReturnsMultipleMedalsUnlock()
    {
        // Arrange
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = GameMode.Standard;
        score.EnrichWithUserData(user);

        score.Mods = Mods.DoubleTime;
        score.MaxCombo = 500;

        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var medalService = Scope.ServiceProvider.GetRequiredService<MedalService>();

        // Act
        var result = await medalService.UnlockAndGetNewMedals(score, beatmap, userStats!);

        // Assert
        Assert.Contains("92+Time And A Half", result);
        Assert.Contains("21+500 Combo", result);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Contains(userMedals, m => m.MedalId == 92);
        Assert.Contains(userMedals, m => m.MedalId == 21);
    }

    [Fact]
    public async Task TestUnlockAndGetNewMedalsWithFailedScoreReturnsEmptyString()
    {
        // Arrange
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = GameMode.Standard;
        score.EnrichWithUserData(user);

        score.IsPassed = false;

        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var medalService = Scope.ServiceProvider.GetRequiredService<MedalService>();

        // Act
        var result = await medalService.UnlockAndGetNewMedals(score, beatmap, userStats!);

        // Assert
        Assert.Equal(string.Empty, result);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Empty(userMedals);
    }

    [Fact]
    public async Task TestUnlockAndGetNewMedalsWithUnscoreableBeatmapReturnsEmptyString()
    {
        // Arrange
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = GameMode.Standard;
        score.EnrichWithUserData(user);

        score.Mods = Mods.NoFail; // This mod would normally unlock the Risk Averse medal, but since the beatmap is unscoreable, it should not unlock any medals.

        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockGraveyardBeatmapWithSetForScore(score);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var medalService = Scope.ServiceProvider.GetRequiredService<MedalService>();

        // Act
        var result = await medalService.UnlockAndGetNewMedals(score, beatmap, userStats!);

        // Assert
        Assert.Equal(string.Empty, result);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Empty(userMedals);
    }

    [Fact]
    public async Task TestUnlockAndGetNewMedalsWithPreviouslyUnlockedMedalReturnsEmptyString()
    {
        // Arrange
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = GameMode.Standard;
        score.EnrichWithUserData(user);

        score.Mods = Mods.NoFail; // This mod would normally unlock the Risk Averse medal, but since we will mock the medal as already unlocked, it should not unlock any new medals.

        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockGraveyardBeatmapWithSetForScore(score);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        await Database.Users.Medals.UnlockMedals(user.Id, [97]);

        var medalService = Scope.ServiceProvider.GetRequiredService<MedalService>();

        // Act
        var result = await medalService.UnlockAndGetNewMedals(score, beatmap, userStats!);

        // Assert
        Assert.DoesNotContain("97+Risk Averse+Safety nets are fun!", result);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Contains(userMedals, m => m.MedalId == 97);
    }
}