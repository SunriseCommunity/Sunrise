using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.Server.API.Managers;
using Sunrise.Server.API.Serializable.Request;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Managers;
using Sunrise.Server.Repositories;
using Sunrise.Server.Services;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;
using AuthService = Sunrise.Server.API.Services.AuthService;

namespace Sunrise.Server.API.Controllers;

[Route("/user")]
[Subdomain("api")]
[ResponseCache(VaryByHeader = "Authorization", Duration = 60)]
public class UserController : ControllerBase
{
    [HttpGet]
    [Route("{id:int}")]
    public async Task<IActionResult> GetUser(int id, [FromQuery(Name = "mode")] int? mode)
    {
        User user;
        var userStatus = "Offline";

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var userSession = sessions.GetSession(userId: id);

        if (userSession != null)
        {
            user = userSession.User;
            user.LastOnlineTime = userSession.Attributes.LastPingRequest;
            userStatus = userSession.Attributes.Status.ToText();
        }
        else
        {
            var userDb = await database.GetUser(id);

            if (userDb == null)
                return NotFound(new ErrorResponse("User not found"));

            user = userDb;
        }

        if (mode == null) return Ok(new UserResponse(user, userStatus));

        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);

        if (isValidMode != true) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        var stats = await database.GetUserStats(id, (GameMode)mode);

        if (stats == null) return NotFound(new ErrorResponse("User stats not found"));

        var globalRank = await database.GetUserRank(id, (GameMode)mode);
        var countryRank = await database.GetUserCountryRank(id, (GameMode)mode);

        var data = JsonSerializer.SerializeToElement(new
        {
            user = new UserResponse(user, userStatus),
            stats = new UserStatsResponse(stats, (int)globalRank, (int)countryRank)
        });

        return Ok(data);
    }

    [HttpGet]
    [Route("self")]
    public async Task<IActionResult> GetSelfUser([FromQuery(Name = "mode")] int? mode)
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        return await GetUser(session.User.Id, mode);
    }

    [HttpPost]
    [Route("edit/description")]
    public async Task<IActionResult> EditDescription([FromBody] EditDescriptionRequest? request)
    {
        if (!ModelState.IsValid || request == null)
            return BadRequest(new ErrorResponse("Description is required"));

        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        if (request.Description!.Length > 2000)
            return BadRequest(new ErrorResponse("Description is too long. Max 2000 characters"));

        session.User.Description = request.Description;

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        await database.UpdateUser(session.User);

        return new OkResult();
    }

    [HttpGet]
    [Route("{id:int}/graph")]
    public async Task<IActionResult> GetUserGraphData(int id, [FromQuery(Name = "mode")] int mode)
    {
        if (mode is < 0 or > 3) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var userStats = await database.GetUserStats(id, (GameMode)mode);

        if (userStats == null) return NotFound(new ErrorResponse("User stats not found"));

        var snapshots = (await database.GetUserStatsSnapshot(id, (GameMode)mode)).GetSnapshots();

        snapshots.RemoveAll(x => x.SavedAt.Date == DateTime.UtcNow.Date);

        snapshots.Add(new StatsSnapshot
        {
            Rank = await database.GetUserRank(id, (GameMode)mode),
            CountryRank = await database.GetUserCountryRank(id, (GameMode)mode),
            PerformancePoints = userStats.PerformancePoints
        });

        snapshots.Sort((a, b) => a.SavedAt.CompareTo(b.SavedAt));

        snapshots = snapshots.TakeLast(60).ToList();

        return Ok(new StatsSnapshotsResponse(snapshots));
    }

    [HttpGet]
    [Route("{id:int}/scores")]
    public async Task<IActionResult> GetUserGraphScores(int id,
        [FromQuery(Name = "mode")] int mode,
        [FromQuery(Name = "type")] int? scoresType,
        [FromQuery(Name = "limit")] int? limit = 15,
        [FromQuery(Name = "page")] int? page = 0)
    {
        if (scoresType is < 0 or > 2 or null) return BadRequest(new ErrorResponse("Invalid scores type parameter"));

        if (mode is < 0 or > 3) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(id);

        if (user == null) return NotFound(new ErrorResponse("User not found"));

        var scores = await database.GetUserScores(id, (GameMode)mode, (ScoreTableType)scoresType);

        var offsetScores = scores.Skip(page * limit ?? 0).Take(limit ?? 50).Select(score => new ScoreResponse(score))
            .ToList();

        return Ok(new ScoresResponse(offsetScores, scores.Count));
    }


    [HttpGet]
    [Route("{id:int}/mostplayed")]
    public async Task<IActionResult> GetUserMostPlayedMaps(int id,
        [FromQuery(Name = "mode")] int mode,
        [FromQuery(Name = "limit")] int? limit = 15,
        [FromQuery(Name = "page")] int? page = 0)
    {
        if (mode is < 0 or > 3) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        var session = await Request.GetSessionFromRequest() ?? AuthService.GenerateIpSession(Request);

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(id);

        if (user == null) return NotFound(new ErrorResponse("User not found"));

        var beatmapsIds = await database.GetUserMostPlayedMapsIds(id, (GameMode)mode);

        var offsetBeatmaps = beatmapsIds.Skip(page * limit ?? 0).Take(limit ?? 50).Select(async bId =>
        {
            var beatmapSet = await BeatmapManager.GetBeatmapSet(session, beatmapId: bId);
            var beatmap = beatmapSet?.Beatmaps.FirstOrDefault(b => b.Id == bId);

            return new BeatmapResponse(beatmap, beatmapSet);
        }).Select(task => task.Result).ToList();

        return Ok(new BeatmapsResponse(offsetBeatmaps, beatmapsIds.Count));
    }


    [HttpGet]
    [Route("{id:int}/favourites")]
    public async Task<IActionResult> GetUserGraphScores(int id,
        [FromQuery(Name = "limit")] int? limit = 50,
        [FromQuery(Name = "page")] int? page = 0)
    {
        var session = await Request.GetSessionFromRequest() ?? AuthService.GenerateIpSession(Request);

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(id);

        if (user == null) return NotFound(new ErrorResponse("User not found"));

        var favourites = await database.GetUserFavouriteBeatmaps(id);

        var offsetFavouriteIds = favourites.Skip(page * limit ?? 0).Take(limit ?? 50).ToList();

        var offsetFavourites = offsetFavouriteIds.Select(async setId =>
        {
            var beatmapSet = await BeatmapManager.GetBeatmapSet(session, setId);
            return new BeatmapSetResponse(beatmapSet);
        }).Select(task => task.Result).ToList();

        return Ok(new BeatmapSetsResponse(offsetFavourites, favourites.Count));
    }

    [HttpGet]
    [Route("leaderboard")]
    public async Task<IActionResult> GetLeaderboard(
        [FromQuery(Name = "mode")] int mode,
        [FromQuery(Name = "type")] int? leaderboardType,
        [FromQuery(Name = "limit")] int? limit = 50,
        [FromQuery(Name = "page")] int? page = 0)
    {
        if (leaderboardType is < 0 or > 1 or null)
            return BadRequest(new ErrorResponse("Invalid leaderboard type parameter"));

        if (mode is < 0 or > 3) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var users = await database.GetAllUsers();

        if (users == null) return NotFound(new ErrorResponse("Users not found"));

        var stats = await database.GetAllUserStats((GameMode)mode, (LeaderboardSortType)leaderboardType);

        if (stats == null) return NotFound(new ErrorResponse("Users not found"));

        stats = stats.Where(x => users.Any(u => u.Id == x.UserId)).ToList();

        var offsetUserStats = stats.Skip(page * limit ?? 0).Take(limit ?? 50).ToList();

        var usersWithStats = offsetUserStats.Select(async stats =>
        {
            var user = users.FirstOrDefault(u => u.Id == stats.UserId);

            var globalRank = await database.GetUserRank(user.Id, (GameMode)mode);
            var countryRank = await database.GetUserCountryRank(user.Id, (GameMode)mode);

            return new UserWithStats(new UserResponse(user),
                new UserStatsResponse(stats, (int)globalRank, (int)countryRank));
        }).Select(task => task.Result).ToList();

        return Ok(new LeaderboardResponse(usersWithStats, users.Count));
    }

    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> GetLeaderboard(
        [FromQuery(Name = "query")] string query)
    {
        if (string.IsNullOrEmpty(query)) return BadRequest(new ErrorResponse("Invalid query parameter"));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var users = await database.SearchUsers(query);

        if (users == null) return NotFound(new ErrorResponse("Users not found"));

        return Ok(users.Select(x => new UserResponse(x)));
    }

    [HttpGet]
    [Route("{id:int}/friend/status")]
    public async Task<IActionResult> GetFriendStatus(int id)
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(id);

        if (user == null)
            return NotFound(new ErrorResponse("User not found"));

        if (user.Id == session.User.Id)
            return BadRequest(new ErrorResponse("You can't check your own friend status"));

        var isFollowing = user.FriendsList.Contains(session.User.Id);
        var isFollowed = session.User.FriendsList.Contains(user.Id);

        return Ok(new FriendStatusResponse(isFollowing, isFollowed));
    }

    [HttpPost]
    [Route("{id:int}/friend/status")]
    public async Task<IActionResult> EditFriendStatus(int id, [FromQuery(Name = "action")] string action)
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(id);

        if (user == null)
            return NotFound(new ErrorResponse("User not found"));

        switch (action)
        {
            case "add":
                session.User.AddFriend(user.Id);
                break;
            case "remove":
                session.User.RemoveFriend(user.Id);
                break;
            default:
                return BadRequest(new ErrorResponse("Invalid action parameter"));
        }

        await database.UpdateUser(session.User);

        return new OkResult();
    }

    [HttpGet]
    [Route("{id:int}/medals")]
    public async Task<IActionResult> GetUserMedals(int id,
        [FromQuery(Name = "mode")] int mode)
    {
        if (mode is < 0 or > 3) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var userMedals = await database.GetUserMedals(id, (GameMode)mode);
        var modeMedals = await database.GetMedals((GameMode)mode);

        return Ok(new MedalsResponse(userMedals, modeMedals));
    }

    [HttpPost(RequestType.AvatarUpload)]
    public async Task<IActionResult> SetAvatar()
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        var file = Request.Form.Files[0];
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, Request.HttpContext.RequestAborted);

        var (isSet, error) = await AssetService.SetAvatar(session.User.Id, buffer);

        if (!isSet || error != null)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.AvatarUpload, null, error);
            return BadRequest(new ErrorResponse(error ?? "Failed to set avatar"));
        }

        return new OkResult();
    }

    [HttpPost(RequestType.BannerUpload)]
    public async Task<IActionResult> SetBanner()
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        var file = Request.Form.Files[0];
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, Request.HttpContext.RequestAborted);

        var (isSet, error) = await AssetService.SetBanner(session.User.Id, buffer);

        if (!isSet || error != null)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.BannerUpload, null, error);
            return BadRequest(new ErrorResponse(error ?? "Failed to set banner"));
        }

        return new OkResult();
    }
}