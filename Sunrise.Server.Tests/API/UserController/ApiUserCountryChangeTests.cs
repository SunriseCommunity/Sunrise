using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Objects.Serializable.Events;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserCountryChangeRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    public static IEnumerable<object[]> GetGameModes()
    {
        return Enum.GetValues(typeof(GameMode)).Cast<GameMode>().Select(mode => new object[]
        {
            mode
        });
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestRankChangeAfterCountryChange(GameMode mode)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var firstPlaceRussiaUser = _mocker.User.GetRandomUser();
        firstPlaceRussiaUser.Country = CountryCode.RU;

        var secondPlaceRussiaUser = _mocker.User.GetRandomUser();
        secondPlaceRussiaUser.Country = CountryCode.RU;

        var firstPlaceAmericaThirdPlaceRussiaUserSelf = _mocker.User.GetRandomUser();
        firstPlaceAmericaThirdPlaceRussiaUserSelf.Country = CountryCode.US;

        var mockUserScoresData = new Dictionary<User, int>
        {
            {
                firstPlaceRussiaUser, 3_000
            },
            {
                secondPlaceRussiaUser, 2_000
            },
            {
                firstPlaceAmericaThirdPlaceRussiaUserSelf, 1_000
            }
        };

        foreach (var (user, pp) in mockUserScoresData)
        {
            await CreateTestUser(user);
            await Database.DbContext.Entry(user).Collection(s => s.UserStats).LoadAsync();

            var newScore = _mocker.Score.GetBestScoreableRandomScore();
            newScore.GameMode = mode;
            newScore.PerformancePoints = pp;
            newScore.EnrichWithUserData(user);
            await CreateTestScore(newScore);

            var gamemodeUserStats = user.UserStats.First(s => s.GameMode == mode);

            await gamemodeUserStats.UpdateWithScore(newScore, null, 0);
            var updateUserStatsResult = await Database.Users.Stats.UpdateUserStats(gamemodeUserStats, user);
            if (updateUserStatsResult.IsFailure)
                throw new Exception(updateUserStatsResult.Error);
        }

        var (firstPlaceRussiaUserGlobalRankBeforeUpdate, firstPlaceRussiaUserCountryRankBeforeUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(firstPlaceRussiaUser, mode);
        var (secondPlaceRussiaUserGlobalRankBeforeUpdate, secondPlaceRussiaUserCountryRankBeforeUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(secondPlaceRussiaUser, mode);
        var (firstPlaceAmericaThirdPlaceRussiaUserGlobalRankBeforeUpdate, firstPlaceAmericaThirdPlaceRussiaUserCountryRankBeforeUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(firstPlaceAmericaThirdPlaceRussiaUserSelf, mode);

        var russiaCountryRanksCountBeforeUpdate = await Database.Users.Stats.Ranks.GetCountryRanksCount(mode, CountryCode.RU);
        var americaCountryRanksCountBeforeUpdate = await Database.Users.Stats.Ranks.GetCountryRanksCount(mode, CountryCode.US);

        var tokens = await GetUserAuthTokens(firstPlaceAmericaThirdPlaceRussiaUserSelf);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest
            {
                NewCountry = CountryCode.RU
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await Database.DbContext.Entry(firstPlaceAmericaThirdPlaceRussiaUserSelf).ReloadAsync(); // Reload country on user

        // Assert
        var (firstPlaceRussiaUserGlobalRankAfterUpdate, firstPlaceRussiaUserCountryRankAfterUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(firstPlaceRussiaUser, mode);
        var (secondPlaceRussiaUserGlobalRankAfterUpdate, secondPlaceRussiaUserCountryRankAfterUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(secondPlaceRussiaUser, mode);
        var (firstPlaceAmericaThirdPlaceRussiaUserGlobalRankAfterUpdate, firstPlaceAmericaThirdPlaceRussiaUserCountryRankAfterUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(firstPlaceAmericaThirdPlaceRussiaUserSelf, mode);

        Assert.Equal(firstPlaceRussiaUserGlobalRankBeforeUpdate, firstPlaceRussiaUserGlobalRankAfterUpdate);
        Assert.Equal(secondPlaceRussiaUserGlobalRankBeforeUpdate, secondPlaceRussiaUserGlobalRankAfterUpdate);
        Assert.Equal(firstPlaceAmericaThirdPlaceRussiaUserGlobalRankBeforeUpdate, firstPlaceAmericaThirdPlaceRussiaUserGlobalRankAfterUpdate);

        Assert.Equal(1, firstPlaceRussiaUserCountryRankBeforeUpdate);
        Assert.Equal(2, secondPlaceRussiaUserCountryRankBeforeUpdate);
        Assert.Equal(1, firstPlaceAmericaThirdPlaceRussiaUserCountryRankBeforeUpdate);

        Assert.Equal(1, firstPlaceRussiaUserCountryRankAfterUpdate);
        Assert.Equal(2, secondPlaceRussiaUserCountryRankAfterUpdate);
        Assert.Equal(3, firstPlaceAmericaThirdPlaceRussiaUserCountryRankAfterUpdate);

        var russiaCountryRanksCountAfterUpdate = await Database.Users.Stats.Ranks.GetCountryRanksCount(mode, CountryCode.RU);
        var americaCountryRanksCountAfterUpdate = await Database.Users.Stats.Ranks.GetCountryRanksCount(mode, CountryCode.US);

        Assert.Equal(2, russiaCountryRanksCountBeforeUpdate);
        Assert.Equal(1, americaCountryRanksCountBeforeUpdate);

        Assert.Equal(3, russiaCountryRanksCountAfterUpdate);
        Assert.Equal(0, americaCountryRanksCountAfterUpdate);
    }

    [Fact]
    public async Task TestPromoteOtherUserCountryAfterChange()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        const CountryCode previousCountry = CountryCode.CA;

        var firstPlaceCountrySelfUser = _mocker.User.GetRandomUser();
        firstPlaceCountrySelfUser.Country = previousCountry;

        var secondPlaceCountryShouldBeFirstAfterPromotionUser = _mocker.User.GetRandomUser();
        secondPlaceCountryShouldBeFirstAfterPromotionUser.Country = previousCountry;

        var mockUserScoresData = new Dictionary<User, int>
        {
            {
                firstPlaceCountrySelfUser, 2_000
            },
            {
                secondPlaceCountryShouldBeFirstAfterPromotionUser, 1_000
            }
        };

        foreach (var (user, pp) in mockUserScoresData)
        {
            await CreateTestUser(user);
            await Database.DbContext.Entry(user).Collection(s => s.UserStats).LoadAsync();

            var newScore = _mocker.Score.GetBestScoreableRandomScore();
            newScore.GameMode = GameMode.Standard;
            newScore.PerformancePoints = pp;
            newScore.EnrichWithUserData(user);
            await CreateTestScore(newScore);

            var gamemodeUserStats = user.UserStats.First(s => s.GameMode == GameMode.Standard);

            await gamemodeUserStats.UpdateWithScore(newScore, null, 0);
            var updateUserStatsResult = await Database.Users.Stats.UpdateUserStats(gamemodeUserStats, user);
            if (updateUserStatsResult.IsFailure)
                throw new Exception(updateUserStatsResult.Error);
        }

        var (firstPlaceCountrySelfGlobalRankBeforeUpdate, firstPlaceCountrySelfCountryRankBeforeUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(firstPlaceCountrySelfUser, GameMode.Standard);
        var (secondPlaceCountryShouldBeFirstAfterPromotionUserGlobalRankBeforeUpdate, secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRankBeforeUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(secondPlaceCountryShouldBeFirstAfterPromotionUser, GameMode.Standard);

        var canadaCountryRanksCountBeforeUpdate = await Database.Users.Stats.Ranks.GetCountryRanksCount(GameMode.Standard, CountryCode.CA);
        var albaniaCountryRanksCountBeforeUpdate = await Database.Users.Stats.Ranks.GetCountryRanksCount(GameMode.Standard, CountryCode.AL);

        var tokens = await GetUserAuthTokens(firstPlaceCountrySelfUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest
            {
                NewCountry = CountryCode.AL
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await Database.DbContext.Entry(firstPlaceCountrySelfUser).ReloadAsync(); // Reload country on user

        var (firstPlaceCountrySelfGlobalRankAfterUpdate, firstPlaceCountrySelfCountryRankAfterUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(firstPlaceCountrySelfUser, GameMode.Standard);
        var (secondPlaceCountryShouldBeFirstAfterPromotionUserGlobalRankAfterUpdate, secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRankAfterUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(secondPlaceCountryShouldBeFirstAfterPromotionUser, GameMode.Standard);

        Assert.Equal(firstPlaceCountrySelfGlobalRankBeforeUpdate, firstPlaceCountrySelfGlobalRankAfterUpdate);
        Assert.Equal(secondPlaceCountryShouldBeFirstAfterPromotionUserGlobalRankBeforeUpdate, secondPlaceCountryShouldBeFirstAfterPromotionUserGlobalRankAfterUpdate);

        Assert.Equal(1, firstPlaceCountrySelfCountryRankBeforeUpdate);
        Assert.Equal(1, firstPlaceCountrySelfCountryRankAfterUpdate);

        Assert.Equal(2, secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRankBeforeUpdate);
        Assert.Equal(1, secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRankAfterUpdate);

        var canadaCountryRanksCountAfterUpdate = await Database.Users.Stats.Ranks.GetCountryRanksCount(GameMode.Standard, CountryCode.CA);
        var albaniaCountryRanksCountAfterUpdate = await Database.Users.Stats.Ranks.GetCountryRanksCount(GameMode.Standard, CountryCode.AL);

        Assert.Equal(2, canadaCountryRanksCountBeforeUpdate);
        Assert.Equal(0, albaniaCountryRanksCountBeforeUpdate);

        Assert.Equal(1, canadaCountryRanksCountAfterUpdate);
        Assert.Equal(1, albaniaCountryRanksCountAfterUpdate);
    }


    [Fact]
    public async Task TestPromoteOtherUserCountryAfterChangeInMultipleGameModes()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        const CountryCode previousCountry = CountryCode.CA;

        var firstPlaceCountrySelfUser = _mocker.User.GetRandomUser();
        firstPlaceCountrySelfUser.Country = previousCountry;

        var secondPlaceCountryShouldBeFirstAfterPromotionUser = _mocker.User.GetRandomUser();
        secondPlaceCountryShouldBeFirstAfterPromotionUser.Country = previousCountry;

        var mockUserScoresData = new Dictionary<User, int>
        {
            {
                firstPlaceCountrySelfUser, 2_000
            },
            {
                secondPlaceCountryShouldBeFirstAfterPromotionUser, 1_000
            }
        };

        foreach (var (user, pp) in mockUserScoresData)
        {
            await CreateTestUser(user);
            await Database.DbContext.Entry(user).Collection(s => s.UserStats).LoadAsync();

            foreach (var mode in Enum.GetValues(typeof(GameMode)).Cast<GameMode>())
            {
                var newScore = _mocker.Score.GetBestScoreableRandomScore();
                newScore.GameMode = mode;
                newScore.PerformancePoints = pp;
                newScore.EnrichWithUserData(user);
                await CreateTestScore(newScore);

                var gamemodeUserStats = user.UserStats.First(s => s.GameMode == mode);

                await gamemodeUserStats.UpdateWithScore(newScore, null, 0);
                var updateUserStatsResult = await Database.Users.Stats.UpdateUserStats(gamemodeUserStats, user);
                if (updateUserStatsResult.IsFailure)
                    throw new Exception(updateUserStatsResult.Error);
            }
        }

        List<long> firstPlaceCountryUserCountryRanksInGameModesBefore = [];
        List<long> secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRanksInGameModesBefore = [];

        foreach (var mode in Enum.GetValues(typeof(GameMode)).Cast<GameMode>())
        {
            var (_, firstPlaceCountrySelfCountryRankBeforeUpdate) =
                await Database.Users.Stats.Ranks.GetUserRanks(firstPlaceCountrySelfUser, mode);
            firstPlaceCountryUserCountryRanksInGameModesBefore.Add(firstPlaceCountrySelfCountryRankBeforeUpdate);

            var (_, secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRankBeforeUpdate) =
                await Database.Users.Stats.Ranks.GetUserRanks(secondPlaceCountryShouldBeFirstAfterPromotionUser, mode);
            secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRanksInGameModesBefore.Add(secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRankBeforeUpdate);
        }

        var tokens = await GetUserAuthTokens(firstPlaceCountrySelfUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest
            {
                NewCountry = CountryCode.AL
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await Database.DbContext.Entry(firstPlaceCountrySelfUser).ReloadAsync(); // Reload country on user
        List<long> firstPlaceCountryUserCountryRanksInGameModesAfter = [];
        List<long> secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRanksInGameModesAfter = [];

        foreach (var mode in Enum.GetValues(typeof(GameMode)).Cast<GameMode>())
        {
            var (_, firstPlaceCountrySelfCountryRankAfterUpdate) =
                await Database.Users.Stats.Ranks.GetUserRanks(firstPlaceCountrySelfUser, mode);
            firstPlaceCountryUserCountryRanksInGameModesAfter.Add(firstPlaceCountrySelfCountryRankAfterUpdate);

            var (_, secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRankAfterUpdate) =
                await Database.Users.Stats.Ranks.GetUserRanks(secondPlaceCountryShouldBeFirstAfterPromotionUser, mode);
            secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRanksInGameModesAfter.Add(secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRankAfterUpdate);
        }

        Assert.All(firstPlaceCountryUserCountryRanksInGameModesBefore, rank => Assert.Equal(1, rank));
        Assert.All(secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRanksInGameModesBefore, rank => Assert.Equal(2, rank));

        Assert.All(firstPlaceCountryUserCountryRanksInGameModesAfter, rank => Assert.Equal(1, rank));
        Assert.All(secondPlaceCountryShouldBeFirstAfterPromotionUserCountryRanksInGameModesAfter, rank => Assert.Equal(1, rank));
    }
}

public class ApiUserCountryChangeTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestCountryChangeWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest
            {
                NewCountry = _mocker.User.GetRandomCountryCode()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestCountryChangeWithActiveRestriction()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest
            {
                NewCountry = _mocker.User.GetRandomCountryCode()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestCountryChangeWithoutBody()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/country/change", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Theory]
    [InlineData("1245")]
    [InlineData("-1")]
    [InlineData("01")]
    [InlineData("peppyland")]
    [InlineData("愛")]
    public async Task TestCountryChangeWithInvalidCountry(string newCountry)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var json = $"{{\"new_country\":\"{newCountry}\"}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("user/country/change", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Equal(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestCountryChange()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var newCountry = _mocker.User.GetRandomCountryCode();

        // Act
        var response = await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest
            {
                NewCountry = newCountry
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(user.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(updatedUser.Country, newCountry);
    }

    [Fact]
    public async Task TestChangeCountryTooFrequently()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var newCountry = CountryCode.AD;

        var updateCountryResult = await Database.Users.UpdateUserCountry(user, user.Country, CountryCode.AL);
        if (updateCountryResult.IsFailure)
            throw new Exception(updateCountryResult.Error);

        var lastUserCountryChange = await Database.Events.Users.GetLastUserCountryChangeEvent(user.Id);
        Assert.NotNull(lastUserCountryChange);

        // Act
        var response = await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest
            {
                NewCountry = newCountry
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.ChangeCountryOnCooldown(lastUserCountryChange.Time.AddDays(Configuration.CountryChangeCooldownInDays)), errorResponse?.Detail);
    }

    [Fact]
    public async Task TestChangeCountryToUnknownCountry()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest
            {
                NewCountry = CountryCode.XX
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.CantChangeCountryToUnknown, errorResponse?.Detail);
    }

    [Fact]
    public async Task TestChangeCountryToSameCountry()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var user = _mocker.User.GetRandomUser();
        var newCountry = CountryCode.BR;
        user.Country = newCountry;

        await CreateTestUser(user);
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest
            {
                NewCountry = newCountry
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.CantChangeCountryToTheSameOne, errorResponse?.Detail);
    }

    [Fact]
    public async Task CheckAdditionToEventsAfterCountryChange()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = _mocker.User.GetRandomUser();
        user.Country = CountryCode.HU;
        user = await CreateTestUser(user);

        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest
            {
                NewCountry = CountryCode.AL
            });

        var lastEvent = await Database.Events.Users.GetLastUserCountryChangeEvent(user.Id);
        var data = lastEvent?.GetData<UserCountryChanged>();

        // Assert
        Assert.NotNull(lastEvent);
        Assert.Equal(CountryCode.AL, data!.NewCountry);
        Assert.Equal(CountryCode.HU, data.OldCountry);
        Assert.Equal(user!.Id, data.UpdatedById);
    }
}