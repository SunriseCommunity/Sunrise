using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Managers;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.API.Services;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using AuthService = Sunrise.API.Services.AuthService;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.API.Controllers;

[Route("/user")]
[Subdomain("api")]
[ResponseCache(VaryByHeader = "Authorization", Duration = 10)]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
public class UserController(SessionManager sessionManager, BeatmapService beatmapService, DatabaseService database, SessionRepository sessions, AssetService assetService) : ControllerBase
{
    [HttpGet]
    [Route("{id:int}")]
    [EndpointDescription("Get user profile. Include mode query to also get user stats")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUser(int id, [FromQuery(Name = "mode")] GameMode? mode, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var user = await database.Users.GetUser(id, ct: ct);
        if (user == null)
            return NotFound(new ErrorResponse("User not found"));

        if (user.IsRestricted())
            return NotFound(new ErrorResponse("User is restricted"));

        if (mode == null) return Ok(new UserResponse(sessions, user));

        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);
        if (isValidMode != true) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        var stats = await database.Users.Stats.GetUserStats(id, (GameMode)mode, ct);

        if (stats == null)
            return NotFound(new ErrorResponse("User stats not found"));

        var (globalRank, countryRank) = await database.Users.Stats.Ranks.GetUserRanks(user, (GameMode)mode, ct: ct);

        var data = JsonSerializer.SerializeToElement(new UserWithStatsResponse(new UserResponse(sessions, user), new UserStatsResponse(stats, (int)globalRank, (int)countryRank)));

        return Ok(data);
    }

    [HttpGet]
    [Route("self")]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Same as /user/{id}, but automatically gets id of current user")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSelfUser([FromQuery(Name = "mode")] GameMode? mode, CancellationToken ct = default)
    {
        var session = await sessionManager.GetSessionFromRequest(Request, ct);
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        return await GetUser(session.UserId, mode, ct);
    }

    [HttpPost]
    [Route("edit/description")]
    [EndpointDescription("Update current user's description")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EditDescription([FromBody] EditDescriptionRequest? request)
    {
        if (!ModelState.IsValid || request == null)
            return BadRequest(new ErrorResponse("Description is required"));

        var session = await sessionManager.GetSessionFromRequest(Request);
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        var user = await database.Users.GetUser(id: session.UserId);
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        if (request.Description!.Length > 2000)
            return BadRequest(new ErrorResponse("Description is too long. Max 2000 characters"));

        user.Description = request.Description;

        await database.Users.UpdateUser(user);

        return new OkResult();
    }

    [HttpGet]
    [Route("{userId:int}/graph")]
    [EndpointDescription("Get user stats graph data")]
    [ResponseCache(VaryByHeader = "Authorization", Duration = 300)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(StatsSnapshotsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserGraphData(int userId, [FromQuery(Name = "mode")] GameMode mode, CancellationToken ct = default)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var user = await database.Users.GetUser(userId, ct: ct);
        if (user == null) return NotFound(new ErrorResponse("User not found"));

        if (user.IsRestricted())
            return NotFound(new ErrorResponse("User is restricted"));

        var userStats = await database.Users.Stats.GetUserStats(userId, mode, ct);

        if (userStats == null) return NotFound(new ErrorResponse("User stats not found"));

        var snapshots = (await database.Users.Stats.Snapshots.GetUserStatsSnapshot(userId, mode, ct)).GetSnapshots();

        var (globalRank, countryRank) = await database.Users.Stats.Ranks.GetUserRanks(user, mode, ct: ct);

        snapshots.Add(new StatsSnapshot
        {
            Rank = globalRank,
            CountryRank = countryRank,
            PerformancePoints = userStats.PerformancePoints
        });

        snapshots.Sort((a, b) => a.SavedAt.CompareTo(b.SavedAt));

        snapshots = snapshots.TakeLast(60).ToList();

        return Ok(new StatsSnapshotsResponse(snapshots));
    }

    [HttpGet]
    [Route("{id:int}/scores")]
    [EndpointDescription("Get user scores")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ScoresResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserScores(int id,
        [FromQuery(Name = "mode")] GameMode mode = 0,
        [FromQuery(Name = "type")] ScoreTableType scoresType = 0,
        [FromQuery(Name = "limit")] int limit = 15,
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken ct = default)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page is <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var user = await database.Users.GetUser(id, ct: ct);

        if (user == null) return NotFound(new ErrorResponse("User not found"));

        if (user.IsRestricted())
            return NotFound(new ErrorResponse("User is restricted"));

        var (scores, totalScores) = await database.Scores.GetUserScores(id,
            mode,
            scoresType,
            new QueryOptions(true, new Pagination(page, limit))
            {
                QueryModifier = query => query.Cast<Score>().IncludeUser()
            },
            ct);

        scores = await database.Scores.EnrichScoresWithLeaderboardPosition(scores, ct);

        var parsedScores = scores.Select(score => new ScoreResponse(sessions, score))
            .ToList();

        return Ok(new ScoresResponse(parsedScores, totalScores));
    }

    [HttpGet]
    [Route("{id:int}/mostplayed")]
    [EndpointDescription("Get user most played beatmaps")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MostPlayedResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserMostPlayedMaps(int id,
        [FromQuery(Name = "mode")] GameMode mode,
        [FromQuery(Name = "limit")] int limit = 15,
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken ct = default)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page is <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var session = await sessionManager.GetSessionFromRequest(Request, ct) ?? AuthService.GenerateIpSession(Request);

        var user = await database.Users.GetUser(id, ct: ct);

        if (user == null) return NotFound(new ErrorResponse("User not found"));

        if (user.IsRestricted())
            return NotFound(new ErrorResponse("User is restricted"));

        var (beatmapsIds, totalIdsCount) = await database.Scores.GetUserMostPlayedBeatmapIds(id, mode, new QueryOptions(true, new Pagination(page, limit)), ct);

        var parsedBeatmaps = beatmapsIds.Select(async pair =>
        {
            var bId = pair.Key;
            var count = pair.Value;

            var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapId: bId, ct: ct);
            if (beatmapSetResult.IsFailure)
                return null;

            var beatmapSet = beatmapSetResult.Value;

            var beatmap = beatmapSet?.Beatmaps.FirstOrDefault(b => b.Id == bId);

            return beatmap == null ? null : new MostPlayedBeatmapResponse(session, beatmap, count, beatmapSet);
        }).Select(task => task.Result).Where(x => x != null).ToList();

        return Ok(new MostPlayedResponse(parsedBeatmaps, totalIdsCount));
    }

    [HttpGet]
    [Route("{id:int}/favourites")]
    [EndpointDescription("Get user favourited beatmapsets")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BeatmapSetsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserFavourites(int id,
        [FromQuery(Name = "limit")] int limit = 50,
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken ct = default)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var session = await sessionManager.GetSessionFromRequest(Request, ct) ?? AuthService.GenerateIpSession(Request);

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page is <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var user = await database.Users.GetUser(id, ct: ct);

        if (user == null) return NotFound(new ErrorResponse("User not found"));

        if (user.IsRestricted())
            return NotFound(new ErrorResponse("User is restricted"));

        var favouritesCount = await database.Users.Favourites.GetUserFavouriteBeatmapIdsCount(id, ct);
        var favourites = await database.Users.Favourites.GetUserFavouriteBeatmapIds(id, new QueryOptions(true, new Pagination(page, limit)), ct);

        var parsedFavourites = favourites.Select(async setId =>
        {
            var beatmapSetResult = await beatmapService.GetBeatmapSet(session, setId, ct: ct);
            if (beatmapSetResult.IsFailure)
                return null;

            var beatmapSet = beatmapSetResult.Value;

            return beatmapSet == null ? null : new BeatmapSetResponse(session, beatmapSet);
        }).Select(task => task.Result).Where(x => x != null).ToList();

        return Ok(new BeatmapSetsResponse(parsedFavourites, favouritesCount));
    }

    [HttpGet]
    [Route("leaderboard")]
    public async Task<IActionResult> GetLeaderboard(
        [FromQuery(Name = "mode")] GameMode mode,
        [FromQuery(Name = "type")] LeaderboardSortType leaderboardType,
        [FromQuery(Name = "limit")] int limit = 50,
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken ct = default)
    {

        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page is <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var countUsers = await database.Users.CountValidUsers(ct);

        var stats = await database.Users.Stats.GetUsersStats(mode,
            leaderboardType,
            options: new QueryOptions(true, new Pagination(page, limit))
            {
                QueryModifier = query => query.Cast<UserStats>().IncludeUser()
            },
            ct: ct);

        if (stats.Count <= 0) return NotFound(new ErrorResponse("User stats not found"));

        var usersWithStatsTask = stats.Select(async userStats =>
        {
            var (globalRank, countryRank) = await database.Users.Stats.Ranks.GetUserRanks(userStats.User, mode, ct: ct);

            return new UserWithStats(new UserResponse(sessions, userStats.User),
                new UserStatsResponse(userStats, globalRank, countryRank));
        }).ToList();

        var usersWithStats = await Task.WhenAll(usersWithStatsTask);

        return Ok(new LeaderboardResponse(usersWithStats.ToList(), countUsers));
    }

    [HttpGet]
    [Route("search")]
    [EndpointDescription("Search user by query")]
    [ProducesResponseType(typeof(List<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeachUsers(
        [FromQuery(Name = "query")] string query,
        [FromQuery(Name = "limit")] int limit = 50,
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrEmpty(query)) return BadRequest(new ErrorResponse("Invalid query parameter"));

        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page is <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var users = await database.Users.GetValidUsersByQueryLike(query, new QueryOptions(true, new Pagination(page, limit)), ct);

        return Ok(users.Select(x => new UserResponse(sessions, x)));
    }

    [HttpGet]
    [Route("friends")]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Get authenticated users friends")]
    [ProducesResponseType(typeof(List<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFriends(
        [FromQuery(Name = "limit")] int limit = 50,
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken ct = default
    )
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var session = await sessionManager.GetSessionFromRequest(Request, ct);
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        var user = await database.Users.GetUser(session.UserId, ct: ct);
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page is <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var (friends, totalCount) = await database.Users.GetUsersFriends(user, new QueryOptions(true, new Pagination(page, limit)), ct);

        return Ok(new FriendsResponse(friends.Select(x => new UserResponse(sessions, x)).ToList(), totalCount));
    }

    [HttpGet]
    [Route("{id:int}/friend/status")]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Get user friendship status")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FriendStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFriendStatus(int id, CancellationToken ct = default)
    {
        var session = await sessionManager.GetSessionFromRequest(Request, ct);
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        var user = await database.Users.GetUser(session.UserId, ct: ct);
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        var requestedUser = await database.Users.GetValidUser(id, ct: ct);
        if (requestedUser == null)
            return NotFound(new ErrorResponse("User not found"));

        if (requestedUser.Id == session.UserId)
            return BadRequest(new ErrorResponse("You can't check your own friend status"));

        var isFollowing = requestedUser.FriendsList.Contains(session.UserId);
        var isFollowed = user.FriendsList.Contains(requestedUser.Id);

        return Ok(new FriendStatusResponse(isFollowing, isFollowed));
    }

    [HttpPost]
    [Route("{id:int}/friend/status")]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Change friendship status with user")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EditFriendStatus(int id, [FromQuery(Name = "action")] string action)
    {
        var session = await sessionManager.GetSessionFromRequest(Request);
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        var user = await database.Users.GetUser(id: session.UserId);
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        var requestedUser = await database.Users.GetValidUser(id);

        if (requestedUser == null)
            return NotFound(new ErrorResponse("User not found"));

        switch (action)
        {
            case "add":
                user.AddFriend(requestedUser.Id);
                break;
            case "remove":
                user.RemoveFriend(requestedUser.Id);
                break;
            default:
                return BadRequest(new ErrorResponse("Invalid action parameter. Use 'add' or 'remove'"));
        }

        var result = await database.Users.UpdateUser(user);
        if (result.IsFailure)
            return BadRequest(result.Error);

        return new OkResult();
    }

    [HttpGet]
    [Route("{id:int}/medals")]
    [EndpointDescription("Get user medals")]
    [ProducesResponseType(typeof(MedalsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserMedals(int id,
        [FromQuery(Name = "mode")] GameMode mode, CancellationToken ct = default)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var userMedals = await database.Users.Medals.GetUserMedals(id, mode, ct: ct);
        var modeMedals = await database.Medals.GetMedals(mode, ct: ct);

        return Ok(new MedalsResponse(userMedals, modeMedals));
    }

    [HttpGet]
    [Route("{id:int}/grades")]
    [EndpointDescription("Get user grades")]
    [ProducesResponseType(typeof(GradesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserGrades(int id,
        [FromQuery(Name = "mode")] GameMode mode, CancellationToken ct = default)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var userGrades = await database.Users.Grades.GetUserGrades(id, mode, ct);

        if (userGrades is null)
            return NotFound(new ErrorResponse("User not found"));

        var user = await database.Users.GetUser(userGrades.UserId, ct: ct);

        if (user == null) return NotFound(new ErrorResponse("User not found"));

        if (user.IsRestricted())
            return NotFound(new ErrorResponse("User is restricted"));

        return Ok(new GradesResponse(userGrades));
    }

    [HttpPost(RequestType.AvatarUpload)]
    [EndpointDescription("Upload new avatar")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetAvatar()
    {
        var session = await sessionManager.GetSessionFromRequest(Request);
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        if (Request.HasFormContentType == false)
            return BadRequest(new ErrorResponse("Invalid content type"));

        if (Request.Form.Files.Count == 0)
            return BadRequest(new ErrorResponse("No files were uploaded"));

        var file = Request.Form.Files[0];
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, Request.HttpContext.RequestAborted);

        var (isSet, error) = await assetService.SetAvatar(session.UserId, buffer);

        if (!isSet || error != null)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.AvatarUpload, null, error);
            return BadRequest(new ErrorResponse(error ?? "Failed to set avatar"));
        }

        return new OkResult();
    }

    [HttpPost(RequestType.BannerUpload)]
    [EndpointDescription("Upload new banner")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetBanner()
    {
        var session = await sessionManager.GetSessionFromRequest(Request);
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        if (Request.HasFormContentType == false)
            return BadRequest(new ErrorResponse("Invalid content type"));

        if (Request.Form.Files.Count == 0)
            return BadRequest(new ErrorResponse("No files were uploaded"));

        var file = Request.Form.Files[0];
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, Request.HttpContext.RequestAborted);

        var (isSet, error) = await assetService.SetBanner(session.UserId, buffer);

        if (!isSet || error != null)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.BannerUpload, null, error);
            return BadRequest(new ErrorResponse(error ?? "Failed to set banner"));
        }

        return new OkResult();
    }

    [HttpPost(RequestType.PasswordChange)]
    [EndpointDescription("Change current password")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest? request)
    {
        var session = await sessionManager.GetSessionFromRequest(Request);
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        var user = await database.Users.GetUser(id: session.UserId);
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        if (!ModelState.IsValid || request == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        if (request.CurrentPassword == null || request.NewPassword == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var userByCurrentPassword = await database.Users.GetUser(passhash: request.CurrentPassword.GetPassHash(), username: user.Username);

        if (userByCurrentPassword == null)
            return BadRequest(new ErrorResponse("Current password is incorrect"));

        var (isPasswordValid, error) = request.NewPassword.IsValidPassword();

        if (!isPasswordValid)
            return BadRequest(new ErrorResponse(error ?? "Invalid password"));

        user.Passhash = request.NewPassword.GetPassHash();

        await database.Users.UpdateUser(user);

        var ip = RegionService.GetUserIpAddress(Request);
        await database.Events.Users.AddUserChangePasswordEvent(user.Id, ip.ToString(), request.CurrentPassword.GetPassHash(), request.NewPassword.GetPassHash());

        return new OkResult();
    }

    [HttpPost(RequestType.UsernameChange)]
    [EndpointDescription("Change current username")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeUsername([FromBody] UsernameChangeRequest? request)
    {
        var session = await sessionManager.GetSessionFromRequest(Request);
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        var user = await database.Users.GetUser(id: session.UserId);
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        if (!ModelState.IsValid || request == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        if (request.NewUsername == null)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var (isUsernameValid, error) = request.NewUsername.IsValidUsername();
        if (!isUsernameValid)
            return BadRequest(new ErrorResponse(error ?? "Invalid username"));

        var lastUsernameChange = await database.Events.Users.GetLastUsernameChangeEvent(session.UserId);
        if (lastUsernameChange != null && lastUsernameChange.Time.AddHours(1) > DateTime.UtcNow)
            return BadRequest(new ErrorResponse("You can change your username only once per hour. Please try again later."));

        var foundUserByUsername = await database.Users.GetUser(username: request.NewUsername);
        if (foundUserByUsername != null && foundUserByUsername.IsActive())
            return BadRequest(new ErrorResponse("Username is already taken"));

        var transactionResult = await database.CommitAsTransactionAsync(async () =>
        {
            if (foundUserByUsername != null)
            {
                var updateFoundUserUsernameResult = await database.Users.UpdateUserUsername(
                    foundUserByUsername,
                    foundUserByUsername.Username,
                    foundUserByUsername.Username.SetUsernameAsOld());

                if (updateFoundUserUsernameResult.IsFailure)
                    throw new ApplicationException("Unexpected error occured while trying to prepare for changing your username.");
            }

            var oldUsername = user.Username;
            user.Username = request.NewUsername;

            var ip = RegionService.GetUserIpAddress(Request);
            var updateUserUsernameResult = await database.Users.UpdateUserUsername(user, oldUsername, request.NewUsername, null, ip.ToString());
            if (updateUserUsernameResult.IsFailure)
                throw new ApplicationException("Unexpected error occured while trying to change your username. Sorry!");
        });

        if (transactionResult.IsFailure)
            return BadRequest(new ErrorResponse(transactionResult.Error));

        return new OkResult();
    }
}