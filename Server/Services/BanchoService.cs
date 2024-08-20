﻿using Sunrise.Server.Data;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public static class BanchoService
{
    public static async Task<string?> GetFriends(string username)
    {
        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(username: username);

        if (user == null)
            return null;

        var friends = user.FriendsList;

        return string.Join("\n", friends);
    }

    public static string GetCurrentEventJson()
    {
        var eventImageUri = $"https://assets.{Configuration.Domain}/events/EventBanner.jpg";

        var json = """{ "images": [{ "image": "{img}", "url": "https://github.com/SunriseCommunity/Sunrise", "IsCurrent": true, "begins": null, "expires": "2099-06-01T12:00:00+00:00"}] }""";
        json = json.Replace("{img}", eventImageUri);
        
        return json;
    }
}