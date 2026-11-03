using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Serializable.Performances;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using Sunrise.Tests.Abstracts;

namespace Sunrise.Shared.Tests.Services;

[Collection("Integration tests collection")]
public class HttpClientRateLimitTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Theory]
    [InlineData(false, 429, false)]
    [InlineData(true, 429, false)]
    [InlineData(false, 429, true)]
    [InlineData(true, 429, true)]
    [InlineData(false, 200, false)]
    [InlineData(true, 200, false)]
    [InlineData(false, 404, false)]
    [InlineData(true, 404, false)]
    public async Task ResponsesPreserveStatusAndOnlyThrottleOn429(bool post, int status, bool jsonOnly)
    {
        var redis = Scope.ServiceProvider.GetRequiredService<RedisRepository>();
        var client = new HttpClientService(redis, NullLogger<HttpClientService>.Instance);
        var type = post ? ApiType.CalculateScorePerformance : ApiType.GetCalculatorVersion;
        var originalApis = Configuration.ExternalApis.ToList();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        await using var upstream = builder.Build();
        upstream.Urls.Add("http://127.0.0.1:0");
        upstream.Run(context =>
        {
            context.Response.StatusCode = jsonOnly ? 200 : status;
            return context.Response.WriteAsJsonAsync(new { status, message = "Upstream response", rosu = "3.2.0" }, timeout.Token);
        });
        await upstream.StartAsync(timeout.Token);
        try
        {
            Configuration.ExternalApis.RemoveAll(api => api.Type == type);
            Configuration.ExternalApis.Add(new ExternalApi(type, ApiServer.Observatory, upstream.Urls.Single(), 0, 0, post));
            var session = BaseSession.GenerateServerSession();
            var result = post
                ? await client.PostRequestWithBody<CalculatorVersionResponse>(session, type, new { }, ct: timeout.Token)
                : await client.SendRequest<CalculatorVersionResponse>(session, type, [], ct: timeout.Token);
            Assert.Equal(status == 200, result.IsSuccess);
            if (result.IsSuccess)
                Assert.Equal("3.2.0", result.Value.Rosu);
            else
                Assert.Equal((HttpStatusCode)status, result.Error.Status);
            Assert.Equal(status == 429, await redis.Get<bool>(RedisKey.ApiServerRateLimited(ApiServer.Observatory)));
        }
        finally
        {
            Configuration.ExternalApis.Clear();
            Configuration.ExternalApis.AddRange(originalApis);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservatoryThrottleRemains429AfterTryingAvailableServers(bool post)
    {
        var redis = Scope.ServiceProvider.GetRequiredService<RedisRepository>();
        await redis.Set(RedisKey.ApiServerRateLimited(ApiServer.Observatory), true, TimeSpan.FromMinutes(1));
        var client = new HttpClientService(redis, NullLogger<HttpClientService>.Instance);
        var session = BaseSession.GenerateServerSession();
        EnvManager.Set("OBSERVATORY_API_KEY", "test-api-key");
        var type = post ? ApiType.CalculateScorePerformance : ApiType.GetCalculatorVersion;
        var original = Configuration.ExternalApis.First(api => api.Type == type);
        var fallback = new ExternalApi(type, ApiServer.Observatory, original.Url, original.Priority + 1, original.NumberOfRequiredArgs, post);
        Configuration.ExternalApis.Add(fallback);
        try
        {
            var result = post
                ? await client.PostRequestWithBody<CalculatorVersionResponse>(session, type, new { })
                : await client.SendRequest<CalculatorVersionResponse>(session, type, []);

            Assert.True(result.IsFailure);
            Assert.Equal(HttpStatusCode.TooManyRequests, result.Error.Status);
        }
        finally
        {
            Configuration.ExternalApis.Remove(fallback);
        }
    }
}
