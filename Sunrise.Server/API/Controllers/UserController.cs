using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.API.Managers;
using Sunrise.Server.API.Serializable.Request;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Extensions;
using Sunrise.Server.Helpers;
using Sunrise.Server.Managers;
using Sunrise.Server.Repositories;
using Sunrise.Server.Services;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;
using AuthService = Sunrise.Server.API.Services.AuthService;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

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
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        User user;
        var userStatus = "Offline";

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var userSession = sessions.GetSession(userId: id);

        if (userSession != null)
        {
            user = userSession.User;
            user.LastOnlineTime = userSession.Attributes.LastPingRequest;
            userStatus = userSession.Attributes.Status.ToText();
        }
        else
        {
            var userDb = await database.UserService.GetUser(id);

            if (userDb == null)
                return NotFound(new ErrorResponse("User not found"));

            user = userDb;
        }

        if (user.IsRestricted())
            return NotFound(new ErrorResponse("User is restricted"));

        if (mode == null) return Ok(new UserResponse(user, userStatus));

        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);
        if (isValidMode != true) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        var stats = await database.UserService.Stats.GetUserStats(id, (GameMode)mode);

        if (stats == null)
            return NotFound(new ErrorResponse("User stats not found"));

        var globalRank = await database.UserService.Stats.GetUserRank(id, (GameMode)mode);
        var countryRank = await database.UserService.Stats.GetUserCountryRank(id, (GameMode)mode);

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

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.UpdateUser(session.User);

        return new OkResult();
    }

    [HttpGet]
    [Route("{userId:int}/graph")]
    public async Task<IActionResult> GetUserGraphData(int userId, [FromQuery(Name = "mode")] int? mode = null)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (mode == null)
            return BadRequest(new ErrorResponse("Mode parameter is required"));

        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);
        if (isValidMode != true) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var user = await database.UserService.GetUser(userId);

        if (user == null) return NotFound(new ErrorResponse("User not found"));

        if (user.IsRestricted())
            return NotFound(new ErrorResponse("User is restricted"));

        var userStats = await database.UserService.Stats.GetUserStats(userId, (GameMode)mode);

        if (userStats == null) return NotFound(new ErrorResponse("User stats not found"));

        var snapshots = (await database.UserService.Stats.Snapshots.GetUserStatsSnapshot(userId, (GameMode)mode)).GetSnapshots();

        snapshots.Add(new StatsSnapshot
        {
            Rank = await database.UserService.Stats.GetUserRank(userId, (GameMode)mode),
            CountryRank = await database.UserService.Stats.GetUserCountryRank(userId, (GameMode)mode),
            PerformancePoints = userStats.PerformancePoints
        });

        snapshots.Sort((a, b) => a.SavedAt.CompareTo(b.SavedAt));

        snapshots = snapshots.TakeLast(60).ToList();

        return Ok(new StatsSnapshotsResponse(snapshots));
    }

    [HttpGet]
    [Route("{id:int}/scores")]
    public async Task<IActionResult> GetUserScores(int id,
        [FromQuery(Name = "mode")] int mode,
        [FromQuery(Name = "type")] int? scoresType,
        [FromQuery(Name = "limit")] int? limit = 15,
        [FromQuery(Name = "page")] int? page = 0)
    {
        if (scoresType is < 0 or > 2 or null) return BadRequest(new ErrorResponse("Invalid scores type parameter"));

        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);
        if (isValidMode != true) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var user = await database.UserService.GetUser(id);

        if (user == null) return NotFound(new ErrorResponse("User not found"));

        var scores = await database.ScoreService.GetUserScores(id, (GameMode)mode, (ScoreTableType)scoresType);

        var offsetScores = scores.Skip(page * limit ?? 0).Take(limit ?? 50).Select(score => new ScoreResponse(score, user))
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
        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);
        if (isValidMode != true) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        var session = await Request.GetSessionFromRequest() ?? AuthService.GenerateIpSession(Request);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var user = await database.UserService.GetUser(id);

        if (user == null) return NotFound(new ErrorResponse("User not found"));

        var beatmapsIds = await database.ScoreService.GetUserMostPlayedBeatmapsIds(id, (GameMode)mode);

        var offsetBeatmaps = beatmapsIds.Skip(page * limit ?? 0).Take(limit ?? 50).Select(async pair =>
        {
            var bId = pair.Key;
            var count = pair.Value;

            var beatmapSet = await BeatmapManager.GetBeatmapSet(session, beatmapId: bId);
            var beatmap = beatmapSet?.Beatmaps.FirstOrDefault(b => b.Id == bId);

            return new MostPlayedBeatmapResponse(session, beatmap, count, beatmapSet);
        }).Select(task => task.Result).ToList();

        return Ok(new MostPlayedResponse(offsetBeatmaps, beatmapsIds.Count));
    }

    [HttpGet]
    [Route("{id:int}/favourites")]
    public async Task<IActionResult> GetUserFavourites(int id,
        [FromQuery(Name = "limit")] int? limit = 50,
        [FromQuery(Name = "page")] int? page = 0)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var session = await Request.GetSessionFromRequest() ?? AuthService.GenerateIpSession(Request);

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page is < 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var user = await database.UserService.GetUser(id);

        if (user == null) return NotFound(new ErrorResponse("User not found"));

        var favourites = await database.UserService.Favourites.GetUserFavouriteBeatmaps(id);

        var offsetFavouriteIds = favourites.Skip(page * limit ?? 0).Take(limit ?? 50).ToList();

        var offsetFavourites = offsetFavouriteIds.Select(async setId =>
        {
            var beatmapSet = await BeatmapManager.GetBeatmapSet(session, setId);
            return beatmapSet == null ? null : new BeatmapSetResponse(session, beatmapSet);
        }).Select(task => task.Result).Where(x => x != null).ToList();

        return Ok(new BeatmapSetsResponse(offsetFavourites, favourites.Count));
    }

    [HttpGet]
    [Route("leaderboard")]
    public async Task<IActionResult> GetLeaderboard(
        [FromQuery(Name = "mode")] int mode,
        [FromQuery(Name = "type")] int? leaderboardType = 0,
        [FromQuery(Name = "limit")] int? limit = 50,
        [FromQuery(Name = "page")] int? page = 0)
    {

        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (Enum.IsDefined(typeof(LeaderboardSortType), leaderboardType) != true)
            return BadRequest(new ErrorResponse("Invalid leaderboard type parameter"));

        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);
        if (isValidMode != true) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page is < 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var users = await database.UserService.GetAllUsers();

        if (users == null) return NotFound(new ErrorResponse("Users not found"));

        var stats = await database.UserService.Stats.GetAllUserStats((GameMode)mode, (LeaderboardSortType)leaderboardType);

        if (stats == null) return NotFound(new ErrorResponse("Users not found"));

        stats = stats.Where(x => users.Any(u => u.Id == x.UserId)).ToList();

        var offsetUserStats = stats.Skip(page * limit ?? 0).Take(limit ?? 50).ToList();

        var usersWithStats = offsetUserStats.Select(async stats =>
        {
            var user = users.FirstOrDefault(u => u.Id == stats.UserId);

            var globalRank = await database.UserService.Stats.GetUserRank(user.Id, (GameMode)mode);
            var countryRank = await database.UserService.Stats.GetUserCountryRank(user.Id, (GameMode)mode);

            return new UserWithStats(new UserResponse(user),
                new UserStatsResponse(stats, (int)globalRank, (int)countryRank));
        }).Select(task => task.Result).ToList();

        return Ok(new LeaderboardResponse(usersWithStats, users.Count));
    }

    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> SeachUsers(
        [FromQuery(Name = "query")] string query,
        [FromQuery(Name = "limit")] int? limit = 50,
        [FromQuery(Name = "page")] int? page = 0
    )
    {
        if (string.IsNullOrEmpty(query)) return BadRequest(new ErrorResponse("Invalid query parameter"));

        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page is < 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var users = await database.UserService.SearchUsers(query);

        if (users == null) return NotFound(new ErrorResponse("Users not found"));

        var offsetUsers = users.Skip(page * limit ?? 0).Take(limit ?? 50).ToList();

        return Ok(offsetUsers.Select(x => new UserResponse(x)));
    }

    [HttpGet]
    [Route("{id:int}/friend/status")]
    public async Task<IActionResult> GetFriendStatus(int id)
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var user = await database.UserService.GetUser(id);

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

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var user = await database.UserService.GetUser(id);

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

        await database.UserService.UpdateUser(session.User);

        return new OkResult();
    }

    [HttpGet]
    [Route("{id:int}/medals")]
    public async Task<IActionResult> GetUserMedals(int id,
        [FromQuery(Name = "mode")] int mode)
    {
        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);
        if (isValidMode != true) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var userMedals = await database.UserService.Medals.GetUserMedals(id, (GameMode)mode);
        var modeMedals = await database.MedalService.GetMedals((GameMode)mode);

        return Ok(new MedalsResponse(userMedals, modeMedals));
    }

    [HttpPost(RequestType.AvatarUpload)]
    public async Task<IActionResult> SetAvatar()
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        if (Request.HasFormContentType == false)
            return BadRequest(new ErrorResponse("Invalid content type"));

        if (Request.Form.Files.Count == 0)
            return BadRequest(new ErrorResponse("No files were uploaded"));

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

        if (Request.HasFormContentType == false)
            return BadRequest(new ErrorResponse("Invalid content type"));

        if (Request.Form.Files.Count == 0)
            return BadRequest(new ErrorResponse("No files were uploaded"));

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

    [HttpPost(RequestType.PasswordChange)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest? request)
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        if (!ModelState.IsValid || request == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        if (request.CurrentPassword == null || request.NewPassword == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var userByCurrentPassword = await database.UserService.GetUser(passhash: request.CurrentPassword.GetPassHash(), username: session.User.Username);

        if (userByCurrentPassword == null)
            return BadRequest(new ErrorResponse("Current password is incorrect"));

        var (isPasswordValid, error) = request.NewPassword.IsValidPassword();

        if (!isPasswordValid)
            return BadRequest(new ErrorResponse(error ?? "Invalid password"));

        session.User.Passhash = request.NewPassword.GetPassHash();

        await database.UserService.UpdateUser(session.User);

        var ip = RegionHelper.GetUserIpAddress(Request);
        await database.EventService.UserEvent.CreateNewUserChangePasswordEvent(session.User.Id, ip.ToString(), request.CurrentPassword.GetPassHash(), request.NewPassword.GetPassHash());

        return new OkResult();
    }

    [HttpPost(RequestType.UsernameChange)]
    public async Task<IActionResult> ChangeUsername([FromBody] UsernameChangeRequest? request)
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        if (!ModelState.IsValid || request == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        if (request.NewUsername == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var (isUsernameValid, error) = request.NewUsername.IsValidUsername();
        if (!isUsernameValid)
            return BadRequest(new ErrorResponse(error ?? "Invalid username"));

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var lastUsernameChange = await database.EventService.UserEvent.GetLastUsernameChange(session.User.Id);
        if (lastUsernameChange != null && lastUsernameChange.Time.AddHours(1) > DateTime.UtcNow)
            return BadRequest(new ErrorResponse("You can change your username only once per hour. Please try again later."));

        var foundUserByUsername = await database.UserService.GetUser(username: request.NewUsername);
        if (foundUserByUsername != null && foundUserByUsername.IsActive())
            return BadRequest(new ErrorResponse("Username is already taken"));

        if (foundUserByUsername != null)
        {
            await database.UserService.UpdateUserUsername(
                foundUserByUsername,
                foundUserByUsername.Username,
                foundUserByUsername.Username.SetUsernameAsOld());
        }

        var oldUsername = session.User.Username;
        session.User.Username = request.NewUsername;

        var ip = RegionHelper.GetUserIpAddress(Request);
        await database.UserService.UpdateUserUsername(session.User, oldUsername, request.NewUsername, null, ip.ToString());

        return new OkResult();
    }
}