using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Services;
using AuthService = Sunrise.API.Services.AuthService;

namespace Sunrise.API.Controllers;

[Route("/auth")]
[Subdomain("api")]
public class AuthController(UserAuthService userAuthService, RegionService regionService) : ControllerBase
{

    [HttpPost("token")]
    public async Task<IActionResult> GetUserToken([FromBody] TokenRequest? request)
    {
        if (!ModelState.IsValid || request == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var user = await database.UserService.GetUser(username: request.Username, passhash: request.Password.GetPassHash());

        if (user == null)
            return BadRequest(new ErrorResponse("Invalid credentials"));

        if (user.IsRestricted())
        {
            var restriction = await database.UserService.Moderation.GetRestrictionReason(user.Id);
            return BadRequest(new ErrorResponse($"Your account is restricted, reason: {restriction}"));
        }

        var location = await regionService.GetRegion(RegionService.GetUserIpAddress(Request));
        if (Configuration.BannedIps.Contains(location.Ip))
            return BadRequest(new ErrorResponse("Your IP address is banned."));

        var token = AuthService.GenerateTokens(user.Id);

        var loginData = new
        {
            RequestHeader = Request.Headers.UserAgent,
            RequestIp = location.Ip,
            RequestCountry = location.Country,
            RequestTime = DateTime.UtcNow
        };

        await database.EventService.UserEvent.CreateNewUserLoginEvent(user.Id, location.Ip, false, loginData);

        return Ok(new TokenResponse(token.Item1, token.Item2, token.Item3));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? request)
    {
        var location = await regionService.GetRegion(RegionService.GetUserIpAddress(Request));
        if (Configuration.BannedIps.Contains(location.Ip))
            return BadRequest(new ErrorResponse("Your IP address is banned."));

        if (!ModelState.IsValid || request == null || request.RefreshToken == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var newToken = AuthService.RefreshToken(request.RefreshToken);
        if (newToken.Item1 == null)
            return BadRequest(new ErrorResponse("Invalid refresh_token provided or user is restricted."));

        return Ok(new RefreshTokenResponse(newToken.Item1, newToken.Item2));
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterRequest? request)
    {
        if (!ModelState.IsValid || request?.Username == null || request.Password == null || request.Email == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var ip = RegionService.GetUserIpAddress(Request);

        var (newUser, errors) = await userAuthService.RegisterUser(request.Username, request.Password, request.Email, ip);

        if (newUser == null)
        {
            var errorString = errors?.FirstOrDefault(x => x.Value.Count != 0).Value.FirstOrDefault() ?? "Unknown error occured.";
            return BadRequest(new ErrorResponse(errorString));
        }

        var token = AuthService.GenerateTokens(newUser.Id);

        return Ok(new TokenResponse(token.Item1, token.Item2, token.Item3));
    }
}