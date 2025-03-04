using Microsoft.AspNetCore.Http;
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
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
public class AuthController(
    UserAuthService userAuthService,
    RegionService regionService,
    AuthService authService,
    DatabaseService database) : ControllerBase
{
    [HttpPost("token")]
    [EndpointDescription("Generate user auth tokens")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserToken([FromBody] TokenRequest? request)
    {
        if (!ModelState.IsValid || request == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var user = await database.Users.GetUser(username: request.Username, passhash: request.Password.GetPassHash());

        if (user == null)
            return BadRequest(new ErrorResponse("Invalid credentials"));

        if (user.IsUserSunriseBot())
            return BadRequest(new ErrorResponse("You can't login as sunrise bot"));

        if (user.IsRestricted())
        {
            var restriction = await database.Users.Moderation.GetActiveRestrictionReason(user.Id);
            return BadRequest(new ErrorResponse($"Your account is restricted, reason: {restriction}"));
        }

        var location = await regionService.GetRegion(RegionService.GetUserIpAddress(Request));
        if (Configuration.BannedIps.Contains(location.Ip))
            return BadRequest(new ErrorResponse("Your IP address is banned."));

        var tokenResult = await authService.GenerateTokens(user.Id);
        if (tokenResult.IsFailure)
            return BadRequest(new ErrorResponse(tokenResult.Error));

        var token = tokenResult.Value;

        var loginData = new
        {
            RequestHeader = Request.Headers.UserAgent,
            RequestIp = location.Ip,
            RequestCountry = location.Country,
            RequestTime = DateTime.UtcNow
        };

        await database.Events.Users.AddUserLoginEvent(user.Id, location.Ip, false, loginData);

        return Ok(new TokenResponse(token.Item1, token.Item2, token.Item3));
    }

    [HttpPost("refresh")]
    [EndpointDescription("Refresh user auth token")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? request)
    {
        var location = await regionService.GetRegion(RegionService.GetUserIpAddress(Request));
        if (Configuration.BannedIps.Contains(location.Ip))
            return BadRequest(new ErrorResponse("Your IP address is banned."));

        if (!ModelState.IsValid || request == null || request.RefreshToken == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var newTokenResult = await authService.RefreshToken(request.RefreshToken);
        if (newTokenResult.IsFailure)
            return BadRequest(new ErrorResponse(newTokenResult.Error));

        var newToken = newTokenResult.Value;

        return Ok(new RefreshTokenResponse(newToken.Item1, newToken.Item2));
    }

    [HttpPost("register")]
    [EndpointDescription("Register new user")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
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

        var tokenResult = await authService.GenerateTokens(newUser.Id);
        if (tokenResult.IsFailure)
            return BadRequest(new ErrorResponse(tokenResult.Error));

        var token = tokenResult.Value;

        return Ok(new TokenResponse(token.Item1, token.Item2, token.Item3));
    }
}