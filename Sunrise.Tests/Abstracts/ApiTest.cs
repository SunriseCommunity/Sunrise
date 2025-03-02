using Microsoft.Extensions.DependencyInjection;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Users;
using AuthService = Sunrise.API.Services.AuthService;

namespace Sunrise.Tests.Abstracts;

public abstract class ApiTest(bool useRedis = false) : DatabaseTest(useRedis)
{
    protected async Task<TokenResponse> GetUserAuthTokens(User? user = null)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();

        user ??= await CreateTestUser();
        var token = authService.GenerateTokens(user.Id);

        return new TokenResponse(token.Item1, token.Item2, token.Item3);
    }
}