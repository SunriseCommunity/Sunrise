using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.API.Serializable.Request;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
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
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(username: request.Username, passhash: request.Password.GetPassHash());

        if (user == null)
            return BadRequest(new ErrorResponse("Invalid credentials"));

        var token = AuthService.GenerateTokens(user.Id);

        return Ok(new TokenResponse(token.Item1, token.Item2, token.Item3));
    }

    [HttpPost("refresh")]
    public IActionResult RefreshToken([FromBody] RefreshTokenRequest? request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var newToken = AuthService.RefreshToken(request.RefreshToken);
        if (newToken.Item1 == null)
            return BadRequest(new ErrorResponse("Invalid refresh_token provided"));

        return Ok(new RefreshTokenResponse(newToken.Item1, newToken.Item2));
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterRequest? request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(username: request.Username);

        if (user != null)
            return BadRequest(new ErrorResponse("Username already in use"));

        user = await database.GetUser(email: request.Email);
        if (user != null)
            return BadRequest(new ErrorResponse("Email already in use"));

        if (!CharactersFilter.IsValidString(request.Username!, true))
            return BadRequest(new ErrorResponse("Invalid characters in username."));

        if (request.Username.Length is < 2 or > 32)
            return BadRequest(new ErrorResponse("Username length should be between 2 and 32 characters."));

        if (!CharactersFilter.IsValidString(request.Email!) || !request.Email.IsValidEmail())
            return BadRequest(new ErrorResponse("Invalid email address."));

        if (!CharactersFilter.IsValidString(request.Password))
            return BadRequest(new ErrorResponse("Invalid characters in password."));

        if (request.Password.Length is < 8 or > 32)
            return BadRequest(new ErrorResponse("Password length should be between 8 and 32 characters."));

        var location = await RegionHelper.GetRegion(RegionHelper.GetUserIpAddress(Request));

        user = new User
        {
            Username = request.Username,
            Passhash = request.Password.GetPassHash(),
            Country = RegionHelper.GetCountryCode(location.Country),
            Email = request.Email,
            Privilege = UserPrivileges.User
        };

        await database.InsertUser(user);

        var token = AuthService.GenerateTokens(user.Id);

        return Ok(new TokenResponse(token.Item1, token.Item2, token.Item3));
    }
}