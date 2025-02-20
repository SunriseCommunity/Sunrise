using System.Net;
using System.Text.Json;
using Sunrise.API.Serializable.Response;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Shared.Application;

namespace Sunrise.Server.Tests.API.BaseController;

public class ApiBaseLimitsTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestLimitsReturnsValidInfo()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");
        var ip = _mocker.User.GetRandomIp();

        // Act
        var response = await client.UseUserIp(ip).GetAsync("/limits");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<LimitsResponse>(responseString);
        Assert.NotNull(status);

        var currentRateLimit = status.RateLimitsObj.TotalLimit - 1;
        Assert.True(currentRateLimit == status.RateLimitsObj.RemainingCalls);
    }

    [Fact]
    public async Task TestLimitsReturnsValidInfoAfterTwoRequests()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");
        var ip = _mocker.User.GetRandomIp();

        // Act
        await client.UseUserIp(ip).GetAsync("/limits");
        var response = await client.UseUserIp(ip).GetAsync("/limits");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<LimitsResponse>(responseString);
        Assert.NotNull(status);

        var currentRateLimit = status.RateLimitsObj.TotalLimit - 2;
        Assert.True(currentRateLimit == status.RateLimitsObj.RemainingCalls);
    }

    [Fact]
    public async Task TestLimitsReturnsTooManyRequestsAfterGettingOverLimit()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");
        var ip = _mocker.User.GetRandomIp();

        // Act
        for (var i = 0; i < Configuration.GeneralCallsPerWindow; i++)
        {
            await client.UseUserIp(ip).GetAsync("/limits");
        }

        var response = await client.UseUserIp(ip).GetAsync("/limits");

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}