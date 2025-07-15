using System.Net;
using System.Net.Http.Json;
using System.Text;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

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

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ErrorResponse>();
        Assert.Contains("authorize to access", responseError?.Error);
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

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ErrorResponse>();
        Assert.Contains("authorize to access", responseError?.Error);
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

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ErrorResponse>();
        Assert.Contains("fields are missing", responseError?.Error);
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

        //I hate this line with all my heart
        var expectedError = "One or more required fields are missing or invalid entry.";

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ErrorResponse>();
        Assert.Equal(expectedError, responseError?.Error);
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

        var newCountry = _mocker.User.GetRandomCountryCode();

        var updateCountryResult = await Database.Users.UpdateUserCountry(user, user.Country, newCountry);
        if (updateCountryResult.IsFailure)
            throw new Exception(updateCountryResult.Error);

        // Act
        var response = await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest
            {
                NewCountry = newCountry
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadFromJsonAsyncWithAppConfig<ErrorResponse>();
        Assert.Contains("unable to change the country", errorResponse?.Error.ToLower());
    }
}

public class ApiUserCountryChangeRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestRankChangeAfterCountryChange()
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

        var (firstPlaceRussiaUserGlobalRankBeforeUpdate, firstPlaceRussiaUserCountryRankBeforeUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(firstPlaceRussiaUser, GameMode.Standard);
        var (secondPlaceRussiaUserGlobalRankBeforeUpdate, secondPlaceRussiaUserCountryRankBeforeUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(secondPlaceRussiaUser, GameMode.Standard);
        var (firstPlaceAmericaThirdPlaceRussiaUserGlobalRankBeforeUpdate, firstPlaceAmericaThirdPlaceRussiaUserCountryRankBeforeUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(firstPlaceAmericaThirdPlaceRussiaUserSelf, GameMode.Standard);

        var russiaCountryRanksCountBeforeUpdate = await Database.Users.Stats.Ranks.GetCountryRanksCount(GameMode.Standard, CountryCode.RU);
        var americaCountryRanksCountBeforeUpdate = await Database.Users.Stats.Ranks.GetCountryRanksCount(GameMode.Standard, CountryCode.US);

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
            await Database.Users.Stats.Ranks.GetUserRanks(firstPlaceRussiaUser, GameMode.Standard);
        var (secondPlaceRussiaUserGlobalRankAfterUpdate, secondPlaceRussiaUserCountryRankAfterUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(secondPlaceRussiaUser, GameMode.Standard);
        var (firstPlaceAmericaThirdPlaceRussiaUserGlobalRankAfterUpdate, firstPlaceAmericaThirdPlaceRussiaUserCountryRankAfterUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(firstPlaceAmericaThirdPlaceRussiaUserSelf, GameMode.Standard);

        Assert.Equal(firstPlaceRussiaUserGlobalRankBeforeUpdate, firstPlaceRussiaUserGlobalRankAfterUpdate);
        Assert.Equal(secondPlaceRussiaUserGlobalRankBeforeUpdate, secondPlaceRussiaUserGlobalRankAfterUpdate);
        Assert.Equal(firstPlaceAmericaThirdPlaceRussiaUserGlobalRankBeforeUpdate, firstPlaceAmericaThirdPlaceRussiaUserGlobalRankAfterUpdate);

        Assert.Equal(1, firstPlaceRussiaUserCountryRankBeforeUpdate);
        Assert.Equal(2, secondPlaceRussiaUserCountryRankBeforeUpdate);
        Assert.Equal(1, firstPlaceAmericaThirdPlaceRussiaUserCountryRankBeforeUpdate);

        Assert.Equal(1, firstPlaceRussiaUserCountryRankAfterUpdate);
        Assert.Equal(2, secondPlaceRussiaUserCountryRankAfterUpdate);
        Assert.Equal(3, firstPlaceAmericaThirdPlaceRussiaUserCountryRankAfterUpdate);

        var russiaCountryRanksCountAfterUpdate = await Database.Users.Stats.Ranks.GetCountryRanksCount(GameMode.Standard, CountryCode.RU);
        var americaCountryRanksCountAfterUpdate = await Database.Users.Stats.Ranks.GetCountryRanksCount(GameMode.Standard, CountryCode.US);

        Assert.Equal(2, russiaCountryRanksCountBeforeUpdate);
        Assert.Equal(1, americaCountryRanksCountBeforeUpdate);

        Assert.Equal(3, russiaCountryRanksCountAfterUpdate);
        Assert.Equal(0, americaCountryRanksCountAfterUpdate);
    }
}
