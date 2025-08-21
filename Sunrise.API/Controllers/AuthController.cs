using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Attributes;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Services;
using AuthService = Sunrise.API.Services.AuthService;

namespace Sunrise.API.Controllers;

[ApiController]
[Route("/auth")]
[Subdomain("api")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status400BadRequest)]
public class AuthController(
    UserAuthService userAuthService,
    RegionService regionService,
    AuthService authService,
    DatabaseService database) : ControllerBase
{
    [HttpPost("token")]
    [EndpointDescription("Generate user auth tokens")]
    [IgnoreMaintenance]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserToken([FromBody] TokenRequest request, CancellationToken ct = default)
    {
        var user = await database.Users.GetUser(username: request.Username, passhash: request.Password.GetPassHash(), ct: ct);

        if (user == null || user.IsUserSunriseBot())
            return Problem(title: ApiErrorResponse.Title.UnableToAuthenticate, detail: ApiErrorResponse.Detail.InvalidCredentialsProvided, statusCode: StatusCodes.Status401Unauthorized);

        if (user.IsRestricted())
        {
            var restriction = await database.Users.Moderation.GetActiveRestrictionReason(user.Id, ct);
            return Problem(title: ApiErrorResponse.Title.UnableToAuthenticate, detail: ApiErrorResponse.Detail.YourAccountIsRestricted(restriction), statusCode: StatusCodes.Status403Forbidden);
        }

        var location = await regionService.GetRegion(RegionService.GetUserIpAddress(Request), ct);

        var tokenResult = await authService.GenerateTokens(user.Id);
        if (tokenResult.IsFailure)
            return Problem(title: ApiErrorResponse.Title.UnableToAuthenticate, detail: tokenResult.Error, statusCode: StatusCodes.Status400BadRequest);

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
    [IgnoreMaintenance]
    [EndpointDescription("Refresh user auth token")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var newTokenResult = await authService.RefreshToken(request.RefreshToken);
        if (newTokenResult.IsFailure)
            return Problem(title: ApiErrorResponse.Title.UnableToRefreshAuthToken, detail: newTokenResult.Error, statusCode: StatusCodes.Status400BadRequest);

        var newToken = newTokenResult.Value;

        return Ok(new RefreshTokenResponse(newToken.Item1, newToken.Item2));
    }

    [HttpPost("register")]
    [EndpointDescription("Register new user")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterRequest request)
    {
        var ip = RegionService.GetUserIpAddress(Request);

        var (newUser, errors) = await userAuthService.RegisterUser(request.Username, request.Password, request.Email, ip);

        if (newUser == null)
        {
            var errorString = errors?.FirstOrDefault(x => x.Value.Count != 0).Value.FirstOrDefault();
            return Problem(title: ApiErrorResponse.Title.UnableToRegisterUser, detail: errorString ?? ApiErrorResponse.Detail.UnknownErrorOccurred, statusCode: StatusCodes.Status400BadRequest);
        }

        var tokenResult = await authService.GenerateTokens(newUser.Id);
        if (tokenResult.IsFailure)
            return Problem(title: ApiErrorResponse.Title.UnableToRegisterUser, detail: tokenResult.Error, statusCode: StatusCodes.Status400BadRequest);

        var token = tokenResult.Value;

        return Ok(new TokenResponse(token.Item1, token.Item2, token.Item3));
    }
}