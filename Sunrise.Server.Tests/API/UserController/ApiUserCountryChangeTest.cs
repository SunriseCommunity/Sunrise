using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Server.Services;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;


namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserCountryChangeTest : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestCountryChangeWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest()
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
            new CountryChangeRequest()
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
    [InlineData("12388")]
    [InlineData("01")]
    [InlineData("433")]
    [InlineData("253")]
    [InlineData("peppyland")]
    [InlineData("adada 445 11 3")]
    [InlineData("country code")]
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
        var response = await client.PostAsync($"user/country/change", content);

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
            new CountryChangeRequest()
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
    public async Task TestChangeCountryTooSoon()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var newCountry = _mocker.User.GetRandomCountryCode();

        // Act
        await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest()
            {
                NewCountry = newCountry
            });

        var response = await client.PostAsJsonAsync("user/country/change",
            new CountryChangeRequest()
            {
                NewCountry = newCountry
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadFromJsonAsyncWithAppConfig<ErrorResponse>();
        Assert.Contains("unable to change the country", errorResponse?.Error.ToLower());
    }
}

public class CountryRankChangeAfterCountryChange() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestRankChangeAfterCountryChange()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var firstUser = _mocker.User.GetRandomUser();
        var secondUser = _mocker.User.GetRandomUser();
        var thirdUser = _mocker.User.GetRandomUser();

        firstUser.Country = CountryCode.RU;
        secondUser.Country = CountryCode.US;
        thirdUser.Country = CountryCode.RU;

        firstUser = await CreateTestUser(firstUser);
        secondUser = await CreateTestUser(secondUser);
        thirdUser = await CreateTestUser(thirdUser);

        var tokens = await GetUserAuthTokens(thirdUser);
        client.UseUserAuthToken(tokens);


        var firstUserScore = await CreateTestScore(firstUser);
        firstUserScore.PerformancePoints = 1234;

        var secondUserScore = await CreateTestScore(secondUser);
        secondUserScore.PerformancePoints = 1200;

        var thirdUserScore = await CreateTestScore(thirdUser);
        thirdUserScore.PerformancePoints = 1210;

        await Database.DbContext.Scores.AddAsync(firstUserScore);
        await Database.DbContext.Scores.AddAsync(secondUserScore);
        await Database.DbContext.Scores.AddAsync(thirdUserScore);
        await Database.DbContext.SaveChangesAsync();

        await Database.Users.Stats.Ranks.SetAllUsersRanks(GameMode.Standard);

        var (firstUserGlobalRankBeforeUpdate, firstUserCountryRankBeforeUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(firstUser, GameMode.Standard);
        var (secondUserGlobalRankBeforeUpdate, secondUserCountryRankBeforeUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(secondUser, GameMode.Standard);
        var (thirdUserGlobalRankBeforeUpdate, thirdUserCountryRankBeforeUpdate) =
            await Database.Users.Stats.Ranks.GetUserRanks(thirdUser, GameMode.Standard);
        // Act
        await Database.Users.UpdateUserCountry(secondUser, secondUser.Country, CountryCode.RU);

        var (firstUserGlobalRank, firstUserCountryRank) =
            await Database.Users.Stats.Ranks.GetUserRanks(firstUser, GameMode.Standard);
        var (secondUserGlobalRank, secondUserCountryRank) =
            await Database.Users.Stats.Ranks.GetUserRanks(secondUser, GameMode.Standard);
        var (thirdUserGlobalRank, thirdUserCountryRank) =
            await Database.Users.Stats.Ranks.GetUserRanks(thirdUser, GameMode.Standard);

        // Assert
        Assert.Equal(1, firstUserCountryRank);
        Assert.Equal(2, thirdUserCountryRank);
        Assert.Equal(3, secondUserCountryRank);

        Assert.Equal(1, firstUserCountryRankBeforeUpdate);
        Assert.Equal(1, secondUserCountryRankBeforeUpdate);
        Assert.Equal(2, thirdUserCountryRankBeforeUpdate);
    }
}