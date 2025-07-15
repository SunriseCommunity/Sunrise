using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sunrise.API.Enums;
using Sunrise.API.Extensions;
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
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Helpers;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.API.Controllers;

[Route("/user")]
[Subdomain("api")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
public class UserController(BeatmapService beatmapService, DatabaseService database, SessionRepository sessions, AssetService assetService) : ControllerBase
{
    [HttpGet]
    [Route("{id:int}")]
    [EndpointDescription("Get user profile")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUser(int id, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var isRequestingSelf = id == HttpContext.GetCurrentSession().UserId;
        var user = isRequestingSelf ? HttpContext.GetCurrentUser() : await database.Users.GetUser(id, options: new QueryOptions(true), ct: ct);

        if (user == null)
            return NotFound(new ErrorResponse("User not found"));

        if (user.IsRestricted())
            return NotFound(new ErrorResponse("User is restricted"));

        return Ok(new UserResponse(sessions, user));
    }

    [HttpGet]
    [Route("{id:int}/{mode}")]
    [EndpointDescription("Get user profile with user stats")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UserWithStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserWithStats(int id, GameMode mode, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var user = await database.Users.GetUser(id,
            options: new QueryOptions(true)
            {
                QueryModifier = q => q.Cast<User>().IncludeUserStats(mode)
            },
            ct: ct);

        if (user == null)
            return NotFound(new ErrorResponse("User not found"));

        if (user.IsRestricted())
            return NotFound(new ErrorResponse("User is restricted"));

        var userStats = user.UserStats.FirstOrDefault(m => m.GameMode == mode);

        if (userStats == null)
        {
            userStats = await database.Users.Stats.GetUserStats(id, mode, ct);

            if (userStats == null)
                return NotFound(new ErrorResponse("User stats not found"));
        }

        var (globalRank, countryRank) = await database.Users.Stats.Ranks.GetUserRanks(user, mode, ct: ct);

        return Ok(new UserWithStatsResponse(new UserResponse(sessions, user), new UserStatsResponse(userStats, (int)globalRank, (int)countryRank)));
    }

    [HttpGet]
    [Authorize]
    [Route("self")]
    [EndpointDescription("Same as /user/{id}, but automatically gets id of current user from token")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSelfUser(CancellationToken ct = default)
    {
        var session = HttpContext.GetCurrentSession();

        return await GetUser(session.UserId, ct);
    }

    [HttpGet]
    [Authorize]
    [Route("self/{mode}")]
    [EndpointDescription("Same as /user/{id}/{mode}, but automatically gets id of current user")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(UserWithStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSelfUserWithStats(GameMode mode, CancellationToken ct = default)
    {
        var session = HttpContext.GetCurrentSession();

        return await GetUserWithStats(session.UserId, mode, ct);
    }

    [HttpPost]
    [Authorize]
    [Route("edit/description")]
    [EndpointDescription("Update current users description")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EditDescription([FromBody] EditDescriptionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("Description is required"));

        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        if (request.Description.Length > 2000)
            return BadRequest(new ErrorResponse("Description is too long. Max 2000 characters"));

        user.Description = request.Description;

        await database.Users.UpdateUser(user);

        return new OkResult();
    }

    [HttpPost]
    [Authorize]
    [Route("edit/default-gamemode")]
    [EndpointDescription("Update current users default gamemode")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EditUserDefaultGameMode([FromBody] EditDefaultGameModeRequest request)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        user.DefaultGameMode = request.DefaultGameMode;

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

        var user = await database.Users.GetUser(userId,
            options: new QueryOptions(true)
            {
                QueryModifier = q => q
                    .Cast<User>()
                    .IncludeUserStats(mode)
                    .IncludeUserStatsSnapshots(mode)
            },
            ct: ct);

        if (user == null)
            return NotFound(new ErrorResponse("User not found"));

        if (user.IsRestricted())
            return NotFound(new ErrorResponse("User is restricted"));

        var userStats = user.UserStats.FirstOrDefault(m => m.GameMode == mode);

        if (userStats == null)
        {
            userStats = await database.Users.Stats.GetUserStats(userId, mode, ct);

            if (userStats == null)
                return NotFound(new ErrorResponse("User stats not found"));
        }

        var userSnapshots = user.UserStatsSnapshots.FirstOrDefault(m => m.GameMode == mode);

        if (userSnapshots == null)
        {
            userSnapshots = await database.Users.Stats.Snapshots.GetUserStatsSnapshot(userId, mode, ct);
        }

        var snapshots = userSnapshots.GetSnapshots();

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

        if (page <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

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

        if (page <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var session = HttpContext.GetCurrentSession();

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

            return beatmap == null ? null : new MostPlayedBeatmapResponse(sessions, beatmap, count, beatmapSet);
        }).Select(task => task.Result).Where(x => x != null).Select(x => x!).ToList();

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

        var session = HttpContext.GetCurrentSession();

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var user = await database.Users.GetUser(id, ct: ct);

        if (user == null) return NotFound(new ErrorResponse("User not found"));

        if (user.IsRestricted())
            return NotFound(new ErrorResponse("User is restricted"));

        var (favourites, favouritesCount) = await database.Users.Favourites.GetUserFavouriteBeatmapIds(id, new QueryOptions(true, new Pagination(page, limit)), ct);

        var parsedFavourites = favourites.Select(async setId =>
        {
            var beatmapSetResult = await beatmapService.GetBeatmapSet(session, setId, ct: ct);
            if (beatmapSetResult.IsFailure)
                return null;

            var beatmapSet = beatmapSetResult.Value;

            return beatmapSet == null ? null : new BeatmapSetResponse(sessions, beatmapSet);
        }).Select(task => task.Result).Where(x => x != null).Select(x => x!).ToList();

        return Ok(new BeatmapSetsResponse(parsedFavourites, favouritesCount));
    }

    [HttpGet]
    [Route("leaderboard")]
    [EndpointDescription("Get servers leaderboard")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(LeaderboardResponse), StatusCodes.Status200OK)]
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

        if (page <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

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
    public async Task<IActionResult> SearchUsers(
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

        if (page <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var users = await database.Users.GetValidUsersByQueryLike(query, new QueryOptions(true, new Pagination(page, limit)), ct);

        return Ok(users.Select(x => new UserResponse(sessions, x)));
    }

    [HttpGet]
    [Authorize]
    [Route("friends")]
    [EndpointDescription("Get authenticated users friends")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FriendsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFriends(
        [FromQuery(Name = "limit")] int limit = 50,
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken ct = default
    )
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var (friends, totalCount) = await database.Users.Relationship.GetUserFriends(user.Id,
            new QueryOptions(true, new Pagination(page, limit))
            {
                QueryModifier = q => q.Cast<UserRelationship>()
                    .Include(r => r.Target)
                    .ThenInclude(u => u.UserFiles.Where(f => f.Type == FileType.Avatar || f.Type == FileType.Banner))
            },
            ct);

        return Ok(new FriendsResponse(friends.Select(x => new UserResponse(sessions, x.Target)).ToList(), totalCount));
    }

    [HttpGet]
    [Authorize]
    [Route("followers")]
    [EndpointDescription("Get authenticated users followers")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FollowersResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFollowers(
        [FromQuery(Name = "limit")] int limit = 50,
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken ct = default
    )
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var (followers, totalCount) = await database.Users.Relationship.GetUserFollowers(user.Id,
            new QueryOptions(true, new Pagination(page, limit))
            {
                QueryModifier = q => q.Cast<UserRelationship>()
                    .Include(r => r.User)
                    .ThenInclude(u => u.UserFiles.Where(f => f.Type == FileType.Avatar || f.Type == FileType.Banner))
            },
            ct);

        return Ok(new FollowersResponse(followers.Select(x => new UserResponse(sessions, x.User)).ToList(), totalCount));
    }

    [HttpGet]
    [Authorize]
    [Route("{id:int}/friend/status")]
    [EndpointDescription("Get user friendship status")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FriendStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFriendStatus(int id, CancellationToken ct = default)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        if (id == user.Id)
            return BadRequest(new ErrorResponse("You can't check your own friendship status"));

        var relationship = await database.Users.Relationship.GetUserRelationship(user.Id, id, ct);
        if (relationship == null)
            return NotFound(new ErrorResponse("User not found"));

        var targetRelationship = await database.Users.Relationship.GetUserRelationship(id, user.Id, ct);
        if (targetRelationship == null)
            return NotFound(new ErrorResponse("User not found"));

        var isFollowing = targetRelationship.Relation == UserRelation.Friend;
        var isFollowed = relationship.Relation == UserRelation.Friend;

        return Ok(new FriendStatusResponse(isFollowing, isFollowed));
    }

    [HttpGet]
    [Authorize]
    [Route("inventory/item")]
    [EndpointDescription("Get count of the item in your inventory")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(InventoryItemResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventoryItemCount(ItemType type, CancellationToken ct = default)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        var inventoryItem = await database.Users.Inventory.GetInventoryItem(user.Id, type, ct: ct);

        return Ok(new InventoryItemResponse(type, inventoryItem?.Quantity ?? 0));
    }

    [HttpGet]
    [Route("{id:int}/friends/count")]
    [EndpointDescription("Get user friends counters")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UserRelationsCountersResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserRelationsCounters(int id, CancellationToken ct = default)
    {
        var user = await database.Users.GetValidUser(id, ct: ct);
        if (user == null)
            return NotFound(new ErrorResponse("User not found"));

        var (_, totalFriends) = await database.Users.Relationship.GetUserFriends(id, ct: ct);
        var (_, totalFollowers) = await database.Users.Relationship.GetUserFollowers(id, ct: ct);

        return Ok(new UserRelationsCountersResponse(totalFollowers, totalFriends));
    }

    [HttpPost]
    [Authorize]
    [Route("{id:int}/friend/status")]
    [EndpointDescription("Change friendship status with user")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EditFriendStatus(int id, [FromBody] EditFriendshipStatusRequest request)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        var relationship = await database.Users.Relationship.GetUserRelationship(user.Id, id);
        if (relationship == null)
            return NotFound(new ErrorResponse("User not found"));

        switch (request.Action)
        {
            case UpdateFriendshipStatusAction.Add:
                relationship.Relation = UserRelation.Friend;
                break;
            case UpdateFriendshipStatusAction.Remove:
                relationship.Relation = UserRelation.None;
                break;
            // TODO: Add ability to block user
            default:
                return BadRequest(new ErrorResponse($"Invalid action parameter. Use any of: {Enum.GetNames(typeof(UpdateFriendshipStatusAction)).Aggregate((x, y) => x + "," + y)}"));
        }

        var result = await database.Users.Relationship.UpdateUserRelationship(relationship);
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

    [HttpGet]
    [Route("{id:int}/metadata")]
    [EndpointDescription("Get user metadata")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UserMetadataResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserMetadata(int id, CancellationToken ct = default)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var userMetadata = await database.Users.Metadata.GetUserMetadata(id, ct);

        if (userMetadata is null)
            return NotFound(new ErrorResponse("User not found"));

        var user = await database.Users.GetUser(userMetadata.UserId, ct: ct);

        if (user == null) return NotFound(new ErrorResponse("User not found"));

        if (user.IsRestricted())
            return NotFound(new ErrorResponse("User is restricted"));

        return Ok(new UserMetadataResponse(userMetadata));
    }

    [HttpPost]
    [Authorize]
    [Route("edit/metadata")]
    [EndpointDescription("Update self metadata")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditSelfUserMetadata([FromBody] EditUserMetadataRequest request, CancellationToken ct = default)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var user = HttpContext.GetCurrentUser();

        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        var userMetadata = await database.Users.Metadata.GetUserMetadata(user.Id, ct);

        if (userMetadata is null)
            return NotFound(new ErrorResponse("User metadata not found"));

        var playstyleEnum = JsonStringFlagEnumHelper.CombineFlags(request.Playstyle);

        userMetadata.Playstyle = request.Playstyle != null ? playstyleEnum : userMetadata.Playstyle;

        userMetadata.Location = request.Location ?? userMetadata.Location;
        userMetadata.Interest = request.Interest ?? userMetadata.Interest;
        userMetadata.Occupation = request.Occupation ?? userMetadata.Occupation;

        userMetadata.Telegram = request.Telegram ?? userMetadata.Telegram;
        userMetadata.Twitch = request.Twitch ?? userMetadata.Twitch;
        userMetadata.Twitter = request.Twitter ?? userMetadata.Twitter;
        userMetadata.Discord = request.Discord ?? userMetadata.Discord;
        userMetadata.Website = request.Website ?? userMetadata.Website;

        await database.Users.Metadata.UpdateUserMetadata(userMetadata);

        return new OkResult();
    }

    [HttpPost(RequestType.AvatarUpload)]
    [Authorize]
    [EndpointDescription("Upload new avatar")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetAvatar()
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        if (Request.HasFormContentType == false)
            return BadRequest(new ErrorResponse("Invalid content type"));

        if (Request.Form.Files.Count == 0)
            return BadRequest(new ErrorResponse("No files were uploaded"));

        var file = Request.Form.Files[0];
        await using var stream = file.OpenReadStream();

        var (isSet, error) = await assetService.SetAvatar(user.Id, stream);

        if (!isSet || error != null)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.AvatarUpload, null, error);
            return BadRequest(new ErrorResponse(error ?? "Failed to set avatar"));
        }

        return new OkResult();
    }

    [HttpPost(RequestType.BannerUpload)]
    [Authorize]
    [EndpointDescription("Upload new banner")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetBanner()
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        if (Request.HasFormContentType == false)
            return BadRequest(new ErrorResponse("Invalid content type"));

        if (Request.Form.Files.Count == 0)
            return BadRequest(new ErrorResponse("No files were uploaded"));

        var file = Request.Form.Files[0];
        await using var stream = file.OpenReadStream();

        var (isSet, error) = await assetService.SetBanner(user.Id, stream);

        if (!isSet || error != null)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.BannerUpload, null, error);
            return BadRequest(new ErrorResponse(error ?? "Failed to set banner"));
        }

        return new OkResult();
    }

    [HttpPost(RequestType.PasswordChange)]
    [Authorize]
    [EndpointDescription("Change current password")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

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
    [Authorize]
    [EndpointDescription("Change current username")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeUsername([FromBody] UsernameChangeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are missing."));

        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        var (isUsernameValid, error) = request.NewUsername.IsValidUsername();
        if (!isUsernameValid)
            return BadRequest(new ErrorResponse(error ?? "Invalid username"));

        var lastUsernameChange = await database.Events.Users.GetLastUsernameChangeEvent(user.Id);
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
    
    [HttpPost(RequestType.CountryChange)]
    [Authorize]
    [EndpointDescription("Change current country")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeCountry([FromBody] CountryChangeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are missing or invalid entry."));

        if (request.NewCountry == CountryCode.XX)
            return BadRequest(new ErrorResponse("You cant change country to the unknown country."));

        var user = HttpContext.GetCurrentUser();

        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        var lastUserCountryChange = await database.Events.Users.GetLastUserCountryChangeEvent(user.Id);
        
        if (lastUserCountryChange?.Time.AddDays(Configuration.CountryChangeCooldownInDays) > DateTime.UtcNow)
            return BadRequest(new ErrorResponse($"Unable to change the country. You'll be able to change your country on {lastUserCountryChange.Time.AddDays(Configuration.CountryChangeCooldownInDays)}. Please try again later."));
        
        var ip = RegionService.GetUserIpAddress(Request);
      
        await database.Users.UpdateUserCountry(user, user.Country, request.NewCountry);

        return Ok();
    }
}