using Sunrise.API.Serializable.Response;
using Sunrise.API.Services;
using Sunrise.Shared.Database.Models.User;

namespace Sunrise.Server.Tests.Core.Abstracts;

public abstract class ApiTest(bool useRedis = false) : DatabaseTest(useRedis)
{
    protected async Task<TokenResponse> GetUserAuthTokens(User? user = null)
    {
        user ??= await CreateTestUser();
        var token = AuthService.GenerateTokens(user.Id);

        return new TokenResponse(token.Item1, token.Item2, token.Item3);
    }
}