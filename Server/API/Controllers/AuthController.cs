using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.API.Serializable.Request;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Services;
using Sunrise.Server.Utils;
using AuthService = Sunrise.Server.API.Services.AuthService;

namespace Sunrise.Server.API.Controllers;

[Route("/auth")]
[Subdomain("api")]
public class AuthController : ControllerBase
{
    [HttpPost("token")]
    public async Task<IActionResult> GetUserToken([FromBody] TokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Username and password are required.");

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(username: request.Username, passhash: request.Password.GetPassHash());

        if (user == null)
            return BadRequest("Invalid credentials");

        var token = AuthService.GenerateTokens(user.Id);

        return Ok(new TokenResponse(token.Item1, token.Item2, token.Item3));
    }

    [HttpPost("refresh")]
    public IActionResult RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest("Refresh token is required.");

        var newToken = AuthService.RefreshToken(request.RefreshToken);
        if (newToken.Item1 == null)
            return BadRequest("Invalid refresh_token provided");

        return Ok(new RefreshTokenResponse(newToken.Item1, newToken.Item2));
    }
}