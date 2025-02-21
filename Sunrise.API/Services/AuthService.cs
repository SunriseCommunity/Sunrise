using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.User;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.API.Services;

public static class AuthService
{
    private static string TokenSecret => Configuration.WebTokenSecret;
    private static TimeSpan TokenExpires => Configuration.WebTokenExpiration;

    public static (string, string, int) GenerateTokens(int userId)
    {
        var token = GenerateJwtToken(userId, TokenExpires);
        var refreshToken = GenerateJwtToken(userId, TimeSpan.FromDays(30));

        return (token, refreshToken, TokenExpires.ToSeconds());
    }

    public static async Task<User?> GetUserFromToken(string token)
    {
        ValidateJwtToken(token, out var userId);
        if (userId == null)
            return null;

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var user = await database.UserService.GetUser(userId);

        return user;
    }

    public static (string?, int) RefreshToken(string token)
    {
        var newToken = ValidateJwtToken(token, out var userId) ? GenerateJwtToken(userId!.Value, TokenExpires) : null;

        if (userId == null)
            return (null, 0);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var isUserRestricted = database.UserService.Moderation.IsRestricted(userId.Value).Result;

        return isUserRestricted ? (null, 0) : (newToken, TokenExpires.ToSeconds());
    }

    private static bool ValidateJwtToken(string token, out int? userId)
    {
        userId = null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var result = handler.ValidateToken(token,
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TokenSecret)),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = "Sunrise",
                    ValidAudience = "Sunrise",
                    ClockSkew = TimeSpan.Zero
                },
                out _);

            if (result.Identity is not ClaimsIdentity identity)
                return false;

            var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier) ?? identity.FindFirst("sub");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var id))
                return false;

            var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
            var user = database.UserService.GetUser(id).Result;

            var hashClaim = identity.FindFirst(ClaimTypes.Hash);
            if (hashClaim == null || hashClaim.Value != $"{user.Id}{user.Passhash}".ToHash())
                return false;

            if (user.AccountStatus == UserAccountStatus.Disabled)
            {
                database.UserService.Moderation.EnableUser(user.Id).Wait();
                // TODO: Send message from bot about account being enabled
            }

            userId = id;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateJwtToken(int userId, TimeSpan expires)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var user = database.UserService.GetUser(userId).Result;

        if (user == null)
            throw new Exception("User not found while generating token");

        var token = new JwtSecurityToken(
            "Sunrise",
            "Sunrise",
            [
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

        var user = new User
        {
            Id = ip.GetHashCode(),
            Username = "Guest"
        };

        return new BaseSession(user);
    }
}