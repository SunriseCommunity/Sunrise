using System.Text.Json;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Utils;

namespace Sunrise.Server.Tests.API.BaseController;

public class ApiBaseLimitsTests : ApiTest
{
    [Fact]
    public async Task TestLimitsReturnsValidInfo()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");
        var ip = MockUtil.GetRandomIp();
        
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
        var ip = MockUtil.GetRandomIp();
        
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
        var ip = MockUtil.GetRandomIp();
        
        // Act
        for (var i = 0; i < Configuration.GeneralCallsPerWindow; i++)
        {
            await client.UseUserIp(ip).GetAsync("/limits");
        }
        
        var response = await client.UseUserIp(ip).GetAsync("/limits");
        
        // Assert
        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}