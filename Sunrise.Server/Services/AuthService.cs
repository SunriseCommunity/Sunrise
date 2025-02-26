﻿using System.Security.Cryptography;
using System.Text;
using HOPEless.Bancho;
using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Extensions;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public static class AuthService
{
    public static async Task<IActionResult> Login(HttpRequest request, HttpResponse response)
    {
        var sr = await new StreamReader(request.Body).ReadToEndAsync();
        var loginRequest = Parsers.ParseLogin(sr);
        var ip = RegionHelper.GetUserIpAddress(request);

        response.Headers["cho-protocol"] = "19";
        response.Headers.Connection = "keep-alive";

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var user = await database.UserService.GetUser(username: loginRequest.Username);

        if (user == null)
            return RejectLogin(response, "User with this username does not exist.");

        if (user.Passhash != loginRequest.PassHash)
            return RejectLogin(response, "Invalid credentials.");

        if (Configuration.OnMaintenance && !user.Privilege.HasFlag(UserPrivileges.Admin))
            return RejectLogin(response,
                "Server is currently in maintenance mode. Please try again later.",
                LoginResponses.ServerError);

        if (user.IsRestricted() && await database.UserService.Moderation.IsRestricted(user.Id))
            return RejectLogin(response, "Your account is restricted. Please contact support for more information.");

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        var oldSession = sessions.GetSession(userId: user.Id);

        if (oldSession != null)
        {
            oldSession.SendNotification("You have been logged in from another location. Please try again later.");
            sessions.SoftRemoveSession(oldSession);
        }

        var location = await RegionHelper.GetRegion(ip);
        location.TimeOffset = loginRequest.UtcOffset;

        await database.EventService.UserEvent.CreateNewUserLoginEvent(user.Id, ip.ToString(), true, sr);


        var session = sessions.CreateSession(user, location, loginRequest);

        if (user.AccountStatus == UserAccountStatus.Disabled)
        {
            await database.UserService.Moderation.EnableUser(user.Id);
            session.SendNotification("Welcome back! Your account has been re-enabled. It may take a few seconds to load your data.");
        }

        response.Headers["cho-token"] = session.Token;

        return await ProceedWithLogin(session);
    }

    private static IActionResult RejectLogin(HttpResponse response, string? reason = null,
        LoginResponses code = LoginResponses.InvalidCredentials)
    {
        response.Headers["cho-token"] = "no-token";

        var writer = new PacketHelper();

        if (reason != null)
            writer.WritePacket(PacketType.ServerNotification, reason);

        writer.WritePacket(PacketType.ServerLoginReply, code);

        return new FileContentResult(writer.GetBytesToSend(), "application/octet-stream");
    }

    public static IActionResult Relogin()
    {
        var writer = new PacketHelper();
        writer.WritePacket(PacketType.ServerRestart, 0); // Forces the client to relogin

        return new FileContentResult(writer.GetBytesToSend(), "application/octet-stream");
    }

    private static async Task<IActionResult> ProceedWithLogin(Session session)
    {
        session.SendLoginResponse(LoginResponses.Success);
        session.SendProtocolVersion();
        session.SendPrivilege();
        session.SendExistingChannels();

        var chatChannels = ServicesProviderHolder.GetRequiredService<ChannelRepository>();

        chatChannels.JoinChannel("#osu", session);
        chatChannels.JoinChannel("#announce", session);

        if (session.User.Privilege.HasFlag(UserPrivileges.Admin)) chatChannels.JoinChannel("#staff", session);

        foreach (var channel in chatChannels.GetChannels(session))
        {
            session.SendChannelAvailable(channel);
        }

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        session.SendFriendsList();
        await sessions.SendCurrentPlayers(session);

        if (session.User.SilencedUntil > DateTime.UtcNow)
            session.SendSilenceStatus((int)(session.User.SilencedUntil - DateTime.UtcNow).TotalSeconds);

        sessions.WriteToAllSessions(PacketType.ServerUserPresence, await session.Attributes.GetPlayerPresence());
        sessions.WriteToAllSessions(PacketType.ServerUserData, await session.Attributes.GetPlayerData());

        await session.SendUserData();
        await session.SendUserPresence();

        session.SendNotification(Configuration.WelcomeMessage);

        if (Configuration.OnMaintenance)
            session.SendNotification(
                "Server is currently in maintenance mode. Please keep in mind that some features may not work properly.");

        return new FileContentResult(session.GetContent(), "application/octet-stream");
    }

    public static async Task<IActionResult> Register(HttpRequest request)
    {
        var username = (string)request.Form["user[username]"]!;
        var password = (string)request.Form["user[password]"]!;
        var email = (string)request.Form["user[user_email]"]!;

        var ip = RegionHelper.GetUserIpAddress(request);

        if (string.IsNullOrEmpty(ip.ToString()))
            return new BadRequestObjectResult("Invalid request: Missing IP address");

        var errors = new Dictionary<string, List<string>>
        {
            ["username"] = [],
            ["user_email"] = [],
            ["password"] = []
        };

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
            return new BadRequestObjectResult("Invalid request: Missing parameters");

        var (isUsernameValid, usernameError) = username.IsValidUsername();
        if (!isUsernameValid)
            errors["username"].Add(usernameError ?? "Invalid username");

        if (!CharactersFilter.IsValidStringCharacters(email!) || !email.IsValidEmailCharacters())
            errors["user_email"].Add("Invalid email. It should be a valid email address.");

        var (isPasswordValid, passwordError) = password.IsValidPassword();
        if (!isPasswordValid)
            errors["password"].Add(passwordError ?? "Invalid password");

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var foundUserByEmail = await database.UserService.GetUser(email: email);

        if (foundUserByEmail != null) errors["user_email"].Add("User with this email already exists.");

        var foundUserByUsername = await database.UserService.GetUser(username: username);

        if (foundUserByUsername != null && foundUserByUsername.IsActive()) errors["username"].Add("User with this username already exists.");

        var isUserCreatedAccountBefore = await database.EventService.UserEvent.IsIpCreatedAccountBefore(ip.ToString());
        if (isUserCreatedAccountBefore && !Configuration.IsDevelopment)
            errors["username"].Add("Please don't create multiple accounts. You have been warned.");

        if (errors.Any(x => x.Value.Count > 0))
            return new BadRequestObjectResult(new
            {
                form_error = new
                {
                    user = errors
                }
            });

        if (request.Form["check"] != "0") return new OkObjectResult("");

        var passhash = password.GetPassHash();
        var location = await RegionHelper.GetRegion(ip);

        if (foundUserByUsername != null)
        {
            await database.UserService.UpdateUserUsername(
                foundUserByUsername,
                foundUserByUsername.Username,
                foundUserByUsername.Username.SetUsernameAsOld());
        }

        var newUser = new User
        {
            Username = username!,
            Email = email!,
            Passhash = passhash,
            Country = RegionHelper.GetCountryCode(location.Country),
            Privilege = UserPrivileges.User
        };

        newUser = await database.UserService.InsertUser(newUser);

        await database.EventService.UserEvent.CreateNewUserRegisterEvent(newUser.Id, ip.ToString(), newUser);

        return new OkObjectResult("");
    }

    public static string GetPassHash(this string password)
    {
        var data = MD5.HashData(Encoding.UTF8.GetBytes(password));
        var sb = new StringBuilder();

        foreach (var b in data)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }
}