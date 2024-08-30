using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Utils;

namespace Sunrise.Server.API.Services;

public static class AuthService
{
    private static string TokenSecret => Configuration.WebTokenSecret;
    private static DateTime TokenExpires => Configuration.WebTokenExpiration;

    public static (string, string, int) GenerateTokens(int userId)
    {
        var token = GenerateJwtToken(userId, TokenExpires);
        var refreshToken = GenerateJwtToken(userId, DateTime.UtcNow.AddMonths(1));

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
        return (!ValidateJwtToken(token, out var userId) ? null : GenerateJwtToken(userId!.Value, TokenExpires), TokenExpires.ToSeconds());
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

    private static string GenerateJwtToken(int userId, DateTime expires)
    {
        var token = new JwtSecurityToken(
            "Sunrise",
            "Sunrise",
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            },
            expires: expires,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TokenSecret)), SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}