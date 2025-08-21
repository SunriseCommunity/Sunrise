using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.API.Services;

public class AuthService(DatabaseService database)
{
    private static string TokenSecret => Configuration.WebTokenSecret;
    private static TimeSpan TokenExpires => Configuration.WebTokenExpiration;

    public async Task<Result<(string, string, int)>> GenerateTokens(int userId)
    {
        var tokenResult = await GenerateJwtToken(userId, TokenExpires);
        if (tokenResult.IsFailure)
            return Result.Failure<(string, string, int)>(tokenResult.Error);

        var token = tokenResult.Value;

        var refreshTokenResult = await GenerateJwtToken(userId, TimeSpan.FromDays(30));
        if (refreshTokenResult.IsFailure)
            return Result.Failure<(string, string, int)>(refreshTokenResult.Error);

        var refreshToken = refreshTokenResult.Value;

        return (token, refreshToken, TokenExpires.ToSeconds());
    }

    public async Task<Result<(string, int)>> RefreshToken(string token)
    {
        var userIdResult = await ValidateJwtToken(token);
        if (!userIdResult.IsSuccess)
            return Result.Failure<(string, int)>("Invalid refresh_token");

        var userId = userIdResult.Value;

        var isUserRestricted = await database.Users.Moderation.IsUserRestricted(userId);
        if (isUserRestricted)
            return Result.Failure<(string, int)>("User is restricted.");

        var newTokenResult = await GenerateJwtToken(userId, TokenExpires);
        if (newTokenResult.IsFailure)
            return Result.Failure<(string, int)>("Error occurred while refreshing token.");

        var newToken = newTokenResult.Value;

        return (newToken, TokenExpires.ToSeconds());
    }

    private async Task<Result<int>> ValidateJwtToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var result = handler.ValidateToken(token,
                Configuration.WebTokenValidationParameters,
                out _);

            if (result.Identity is not ClaimsIdentity identity)
                return Result.Failure<int>("Invalid token");

            var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier) ?? identity.FindFirst("sub");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var id))
                return Result.Failure<int>("Invalid token");

            var user = await database.Users.GetUser(id);
            if (user == null)
                return Result.Failure<int>("User not found");

            var hashClaim = identity.FindFirst(ClaimTypes.Hash);
            if (hashClaim == null || hashClaim.Value != $"{user.Id}{user.Passhash}".ToHash())
                return Result.Failure<int>("Invalid user password");

            return id;
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(ex.Message);
        }
    }

    private async Task<Result<string>> GenerateJwtToken(int userId, TimeSpan expires)
    {
        var user = await database.Users.GetUser(userId);

        if (user == null)
        {
            return Result.Failure<string>("User not found");
        }

        var token = new JwtSecurityToken(
            "Sunrise",
            "Sunrise",
            [
                new Claim(ClaimTypes.Name, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Hash, $"{userId}{user.Passhash}".ToHash())
            ],
            expires: DateTime.UtcNow.Add(expires),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TokenSecret)),
                SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static BaseSession GenerateIpSession(HttpRequest request)
    {
        var ip = RegionService.GetUserIpAddress(request);

        return BaseSession.GenerateGuestSession(ip);
    }
}