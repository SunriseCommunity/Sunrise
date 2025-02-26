using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.API.Serializable.Request;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Extensions;
using Sunrise.Server.Helpers;
using Sunrise.Server.Services;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;
using AuthService = Sunrise.Server.API.Services.AuthService;

namespace Sunrise.Server.API.Controllers;

[Route("/auth")]
[Subdomain("api")]
public class AuthController : ControllerBase
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

        var location = await RegionHelper.GetRegion(RegionHelper.GetUserIpAddress(Request));
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
        var location = await RegionHelper.GetRegion(RegionHelper.GetUserIpAddress(Request));
        if (Configuration.BannedIps.Contains(location.Ip))
            return BadRequest(new ErrorResponse("Your IP address is banned."));

        if (!ModelState.IsValid || request == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var newToken = AuthService.RefreshToken(request.RefreshToken);
        if (newToken.Item1 == null)
            return BadRequest(new ErrorResponse("Invalid refresh_token provided or user is restricted."));

        return Ok(new RefreshTokenResponse(newToken.Item1, newToken.Item2));
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterRequest? request)
    {
        if (!ModelState.IsValid || request == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var foundUserByEmail = await database.UserService.GetUser(email: request.Email);
        if (foundUserByEmail != null)
            return BadRequest(new ErrorResponse("Email already in use"));

        var (isUsernameValid, usernameError) = request.Username.IsValidUsername();
        if (!isUsernameValid)
            return BadRequest(new ErrorResponse(usernameError ?? "Invalid username"));

        if (!CharactersFilter.IsValidStringCharacters(request.Email!) || !request.Email.IsValidEmailCharacters())
            return BadRequest(new ErrorResponse("Invalid email address."));

        var (isPasswordValid, passwordError) = request.Password.IsValidPassword();
        if (!isPasswordValid)
            return BadRequest(new ErrorResponse(passwordError ?? "Invalid password"));

        var location = await RegionHelper.GetRegion(RegionHelper.GetUserIpAddress(Request));

        if (Configuration.BannedIps.Contains(location.Ip))
            return BadRequest(new ErrorResponse("Your IP address is banned. Please contact support."));

        var isUserCreatedAccountBefore = await database.EventService.UserEvent.IsIpCreatedAccountBefore(location.Ip);
        if (isUserCreatedAccountBefore && !Configuration.IsDevelopment)
            return BadRequest(new ErrorResponse("Please don't create multiple accounts. You have been warned."));

        var foundUserByUsername = await database.UserService.GetUser(username: request.Username);
        if (foundUserByUsername != null && foundUserByUsername.IsActive())
            return BadRequest(new ErrorResponse("Username is already taken"));

        if (foundUserByUsername != null)
        {
            await database.UserService.UpdateUserUsername(
                foundUserByUsername,
                foundUserByUsername.Username,
                foundUserByUsername.Username.SetUsernameAsOld());
        }

        var newUser = new User
        {
            Username = request.Username,
            Passhash = request.Password.GetPassHash(),
            Country = RegionHelper.GetCountryCode(location.Country),
            Email = request.Email,
            Privilege = UserPrivileges.User
        };

        newUser = await database.UserService.InsertUser(newUser);

        await database.EventService.UserEvent.CreateNewUserRegisterEvent(newUser.Id, location.Ip, newUser);

        var token = AuthService.GenerateTokens(newUser.Id);

        return Ok(new TokenResponse(token.Item1, token.Item2, token.Item3));
    }
}