using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Database.Models.User;
using AuthService = Sunrise.Server.API.Services.AuthService;

namespace Sunrise.Server.Tests.Core.Abstracts;

public abstract class ApiTest : DatabaseTest
{
    protected static async Task<TokenResponse> GetUserAuthTokens(User? user = null)
    {
        user ??= await CreateTestUser();
        var token = AuthService.GenerateTokens(user.Id);

        return new TokenResponse(token.Item1, token.Item2, token.Item3);
    }
}