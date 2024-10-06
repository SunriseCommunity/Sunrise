using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects;
using Sunrise.Server.Utils;

namespace Sunrise.Server.API.Services;

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

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(userId);

        return user;
    }

    public static (string?, int) RefreshToken(string token)
    {
        return (!ValidateJwtToken(token, out var userId) ? null : GenerateJwtToken(userId!.Value, TokenExpires),
            TokenExpires.ToSeconds());
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
        var token = new JwtSecurityToken(
            "Sunrise",
            "Sunrise",
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            ],
            expires: DateTime.UtcNow.Add(expires),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TokenSecret)),
                SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static BaseSession GenerateIpSession(HttpRequest request)
    {
        var ip = RegionHelper.GetUserIpAddress(request);

        var user = new User
        {
            Id = ip.GetHashCode(),
            Username = "Guest"
        };

        return new BaseSession(user);
    }
}