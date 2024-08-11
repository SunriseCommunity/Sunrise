﻿using System.Security.Cryptography;
using System.Text;
using HOPEless.Bancho;
using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.Server.Data;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Chat;
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

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        var user = await database.GetUser(username: loginRequest.Username);
        var location = await RegionHelper.GetRegion(ip);
        location.TimeOffset = loginRequest.UtcOffset;

        if (!CharactersFilter.IsValidString(loginRequest.Username, true))
        {
            return RejectLogin(response, "Invalid characters in username or password.");
        }

        if (user == null)
        {
            return RejectLogin(response, "User with this username does not exist.");
        }

        if (user.Passhash != loginRequest.PassHash)
        {
            return RejectLogin(response, "Invalid credentials.");
        }

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        if (sessions.IsUserOnline(user.Id))
        {
            return RejectLogin(response, "User is already logged in. Try again later.");
        }

        var session = sessions.CreateSession(user, location, loginRequest);

        response.Headers["cho-token"] = session.Token;

        return await ProceedWithLogin(session);
    }

    private static IActionResult RejectLogin(HttpResponse response, string? reason = null)
    {
        response.Headers["cho-token"] = "no-token";

        var writer = new PacketHelper();

        if (reason != null)
        {
            writer.WritePacket(PacketType.ServerNotification, reason);
        }

        writer.WritePacket(PacketType.ServerLoginReply, LoginResponses.InvalidCredentials);

        return new FileContentResult(writer.GetBytesToSend(), "application/octet-stream");
    }

    public static IActionResult Relogin(HttpResponse response, string? reason = null)
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

        var chatChannels = ServicesProviderHolder.ServiceProvider.GetRequiredService<ChannelRepository>();

        chatChannels.JoinChannel("#osu", session);
        chatChannels.JoinChannel("#announce", session);

        if (session.User.Privilege >= PlayerRank.SuperMod)
        {
            chatChannels.JoinChannel("#staff", session);
        }

        foreach (var channel in chatChannels.GetChannels(session))
        {
            session.SendChannelAvailable(channel);
        }

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        session.SendFriendsList();
        await sessions.SendCurrentPlayers(session);

        sessions.WriteToAllSessions(PacketType.ServerUserPresence, await session.Attributes.GetPlayerPresence());
        sessions.WriteToAllSessions(PacketType.ServerUserData, await session.Attributes.GetPlayerData());

        await session.SendUserData();
        await session.SendUserPresence();

        session.SendNotification(Configuration.WelcomeMessage);

        return new FileContentResult(session.GetContent(), "application/octet-stream");
    }

    public static async Task<IActionResult> Register(HttpRequest request)
    {
        var username = (string)request.Form["user[username]"]!;
        var password = (string)request.Form["user[password]"]!;
        var email = (string)request.Form["user[user_email]"]!;

        var ip = RegionHelper.GetUserIpAddress(request);
        var location = await RegionHelper.GetRegion(ip);

        var errors = new Dictionary<string, List<string>>
        {
            ["username"] = [],
            ["user_email"] = [],
            ["password"] = []
        };

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
        {
            return new BadRequestObjectResult("Invalid request: Missing parameters");
        }

        if (!CharactersFilter.IsValidString(username!, true))
        {
            errors["username"].Add("Invalid username. It should contain only alphanumeric characters.");

        }
        else if (username.Length is < 2 or > 32)
        {
            errors["username"].Add("Invalid username. Length should be between 2 and 32 characters.");

        }

        if (!CharactersFilter.IsValidString(email!) || !email.Contains('@') || !email.Contains('.'))
        {
            errors["user_email"].Add("Invalid email. It should contain '@' and '.'.");
        }

        if (!CharactersFilter.IsValidString(password!))
        {
            errors["password"].Add("Invalid password. It should contain only alphanumeric characters.");
        }
        else if (password.Length is < 8 or > 32)
        {
            errors["password"].Add("Invalid password. It should contain between 8 and 32 characters.");
        }

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        var user = await database.GetUser(username: username);

        if (user != null)
        {
            errors["username"].Add("User with this username already exists.");
        }

        user = await database.GetUser(email: email);

        if (user != null)
        {
            errors["user_email"].Add("User with this email already exists.");
        }

        if (errors.Any(x => x.Value.Count > 0))
        {
            return new BadRequestObjectResult(new
            {
                form_error = new
                {
                    user = errors
                }
            });
        }

        if (request.Form["check"] != "0")
        {
            return new OkObjectResult("");
        }

        var data = MD5.HashData(Encoding.UTF8.GetBytes(password));
        var sb = new StringBuilder();

        foreach (var b in data)
        {
            sb.Append(b.ToString("x2"));
        }

        var passhash = sb.ToString();

        user = new User
        {
            Username = username!,
            Email = email!,
            Passhash = passhash,
            Country = RegionHelper.GetCountryCode(location.Country),
            Privilege = PlayerRank.Supporter
        };

        await database.InsertUser(user);

        return new OkObjectResult("");
    }
}