using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sunrise.API.Enums;
using Sunrise.API.Extensions;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.API.Services;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Helpers;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Serializable.Events;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.API.Controllers;

[ApiController]
[Route("/user")]
[Subdomain("api")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status400BadRequest)]
public class UserController(BeatmapService beatmapService, DatabaseService database, SessionRepository sessions, AssetService assetService) : ControllerBase
{
    [HttpGet]
    [Route("{id:int}")]
    [EndpointDescription("Get user profile")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUser([Range(1, int.MaxValue)] int id, CancellationToken ct = default)
    {
        var isRequestingSelf = id == HttpContext.GetCurrentSession().UserId;
        var user = isRequestingSelf ? HttpContext.GetCurrentUser() : await database.Users.GetUser(id, options: new QueryOptions(true), ct: ct);

        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        if (user.IsRestricted())
            return Problem(ApiErrorResponse.Detail.UserIsRestricted, statusCode: StatusCodes.Status404NotFound);

        return Ok(new UserResponse(sessions, user));
    }

    [HttpGet]
    [Route("{id:int}/{mode}")]
    [EndpointDescription("Get user profile with user stats")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UserWithStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserWithStats([Range(1, int.MaxValue)] int id, GameMode mode, CancellationToken ct = default)
    {
        var user = await database.Users.GetUser(id,
            options: new QueryOptions(true)
            {
                QueryModifier = q => q.Cast<User>().IncludeUserStats(mode)
            },
            ct: ct);

        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        if (user.IsRestricted())
            return Problem(ApiErrorResponse.Detail.UserIsRestricted, statusCode: StatusCodes.Status404NotFound);

        var userStats = user.UserStats.FirstOrDefault(m => m.GameMode == mode);

        if (userStats == null)
        {
            userStats = await database.Users.Stats.GetUserStats(id, mode, ct);

            if (userStats == null)
                return Problem(ApiErrorResponse.Detail.UserStatsNotFound, statusCode: StatusCodes.Status404NotFound);
        }

        var (globalRank, countryRank) = await database.Users.Stats.Ranks.GetUserRanks(user, mode, ct: ct);

        return Ok(new UserWithStatsResponse(new UserResponse(sessions, user), new UserStatsResponse(userStats, (int)globalRank, (int)countryRank)));
    }

    [HttpGet]
    [Authorize]
    [Route("self")]
    [EndpointDescription("Same as /user/{id}, but automatically gets id of current user from token")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
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
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
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
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EditDescription([FromBody] EditDescriptionRequest request)
    {
        var user = HttpContext.GetCurrentUserOrThrow();

        user.Description = request.Description;

        await database.Users.UpdateUser(user);

        return new OkResult();
    }

    [HttpPost]
    [Authorize]
    [Route("edit/default-gamemode")]
    [EndpointDescription("Update current users default gamemode")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EditUserDefaultGameMode([FromBody] EditDefaultGameModeRequest request)
    {
        var user = HttpContext.GetCurrentUserOrThrow();

        user.DefaultGameMode = request.DefaultGameMode;

        await database.Users.UpdateUser(user);

        return new OkResult();
    }

    [HttpGet]
    [Route("{userId:int}/graph")]
    [EndpointDescription("Get user stats graph data")]
    [ResponseCache(VaryByHeader = "Authorization", Duration = 300)]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(StatsSnapshotsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserGraphData([Range(1, int.MaxValue)] int userId, [Required] [FromQuery(Name = "mode")] GameMode mode, CancellationToken ct = default)
    {
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
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        if (user.IsRestricted())
            return Problem(ApiErrorResponse.Detail.UserIsRestricted, statusCode: StatusCodes.Status404NotFound);

        var userStats = user.UserStats.FirstOrDefault(m => m.GameMode == mode);

        if (userStats == null)
        {
            userStats = await database.Users.Stats.GetUserStats(userId, mode, ct);

            if (userStats == null)
                return Problem(ApiErrorResponse.Detail.UserStatsNotFound, statusCode: StatusCodes.Status404NotFound);
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
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ScoresResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserScores(
        [Range(1, int.MaxValue)] int id,
        [FromQuery(Name = "mode")] GameMode mode = GameMode.Standard,
        [FromQuery(Name = "type")] ScoreTableType scoresType = ScoreTableType.Best,
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int limit = 15,
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int page = 1,
        CancellationToken ct = default)
    {

        var user = await database.Users.GetUser(id, ct: ct);

        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        if (user.IsRestricted())
            return Problem(ApiErrorResponse.Detail.UserIsRestricted, statusCode: StatusCodes.Status404NotFound);

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
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MostPlayedResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserMostPlayedMaps([Range(1, int.MaxValue)] int id,
        [FromQuery(Name = "mode")] GameMode mode,
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int limit = 15,
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int page = 1,
        CancellationToken ct = default)
    {
        var session = HttpContext.GetCurrentSession();

        var user = await database.Users.GetUser(id, ct: ct);

        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        if (user.IsRestricted())
            return Problem(ApiErrorResponse.Detail.UserIsRestricted, statusCode: StatusCodes.Status404NotFound);

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
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BeatmapSetsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserFavourites([Range(1, int.MaxValue)] int id,
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int limit = 50,
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int page = 1,
        CancellationToken ct = default)
    {
        var session = HttpContext.GetCurrentSession();

        var user = await database.Users.GetUser(id, ct: ct);

        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        if (user.IsRestricted())
            return Problem(ApiErrorResponse.Detail.UserIsRestricted, statusCode: StatusCodes.Status404NotFound);

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

    [HttpGet(RequestType.UserPreviousUsernames)]
    [EndpointDescription("Get previous usernames of the user")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(PreviousUsernamesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserPreviousUsernames([Range(1, int.MaxValue)] int id, CancellationToken ct = default)
    {
        var user = await database.Users.GetUser(id, ct: ct);

        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        if (user.IsRestricted())
            return Problem(ApiErrorResponse.Detail.UserIsRestricted, statusCode: StatusCodes.Status404NotFound);

        var previousUsernames = await database.Events.Users.GetUserPreviousUsernameChangeEvents(user.Id,
            new QueryOptions(true, new Pagination(1, 3))
            {
                QueryModifier = query => query.Cast<EventUser>().OrderByDescending(e => e.Id)
            },
            ct);

        var usernames = previousUsernames.Select(e => e.GetData<UserUsernameChanged>())
            .Where(data =>
            {
                if (data == null) return false;

                var isUsernameFiltered = data.NewUsername.Contains("filtered");
                var isUsernameHidden = data.IsHiddenFromPreviousUsernames != null && data.IsHiddenFromPreviousUsernames.Value;

                return !isUsernameFiltered && !isUsernameHidden;
            })
            .Select(data => data?.OldUsername ?? "Unknown").ToList();

        return Ok(new PreviousUsernamesResponse(usernames));
    }

    [HttpGet]
    [Route("leaderboard")]
    [EndpointDescription("Get servers leaderboard")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(LeaderboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeaderboard(
        [FromQuery(Name = "mode")] GameMode mode,
        [FromQuery(Name = "type")] LeaderboardSortType leaderboardType,
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int limit = 50,
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int page = 1,
        CancellationToken ct = default)
    {
        var countUsers = await database.Users.CountValidUsers(ct);

        var stats = await database.Users.Stats.GetUsersStats(mode,
            leaderboardType,
            options: new QueryOptions(true, new Pagination(page, limit))
            {
                QueryModifier = query => query.Cast<UserStats>().IncludeUser()
            },
            ct: ct);

        if (stats.Count <= 0) return Problem(ApiErrorResponse.Detail.UserStatsNotFound, statusCode: StatusCodes.Status404NotFound);

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
        [Required] [FromQuery(Name = "query")] string query,
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int limit = 50,
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int page = 1,
        CancellationToken ct = default
    )
    {
        var users = await database.Users.GetValidUsersByQueryLike(query, new QueryOptions(true, new Pagination(page, limit)), ct);

        return Ok(users.Select(x => new UserResponse(sessions, x)));
    }

    [HttpGet]
    [Authorize]
    [Route("friends")]
    [EndpointDescription("Get authenticated users friends")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FriendsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFriends(
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int limit = 50,
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int page = 1,
        CancellationToken ct = default
    )
    {
        var user = HttpContext.GetCurrentUserOrThrow();

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
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FollowersResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFollowers(
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int limit = 50,
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int page = 1,
        CancellationToken ct = default
    )
    {
        var user = HttpContext.GetCurrentUserOrThrow();

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
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FriendStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFriendStatus([Range(1, int.MaxValue)] int id, CancellationToken ct = default)
    {
        var user = HttpContext.GetCurrentUserOrThrow();

        if (id == user.Id)
            return Problem(ApiErrorResponse.Detail.CantCheckSelfFriendshipStatus, statusCode: StatusCodes.Status400BadRequest);

        var relationship = await database.Users.Relationship.GetUserRelationship(user.Id, id, ct);
        if (relationship == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        var targetRelationship = await database.Users.Relationship.GetUserRelationship(id, user.Id, ct);
        if (targetRelationship == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        var isFollowing = targetRelationship.Relation == UserRelation.Friend;
        var isFollowed = relationship.Relation == UserRelation.Friend;

        return Ok(new FriendStatusResponse(isFollowing, isFollowed));
    }

    [HttpGet]
    [Authorize]
    [Route("inventory/item")]
    [EndpointDescription("Get count of the item in your inventory")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(InventoryItemResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventoryItemCount([Required] ItemType type, CancellationToken ct = default)
    {
        var user = HttpContext.GetCurrentUserOrThrow();

        var inventoryItem = await database.Users.Inventory.GetInventoryItem(user.Id, type, ct: ct);

        return Ok(new InventoryItemResponse(type, inventoryItem?.Quantity ?? 0));
    }

    [HttpGet]
    [Route("{id:int}/friends/count")]
    [EndpointDescription("Get user friends counters")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UserRelationsCountersResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserRelationsCounters([Range(1, int.MaxValue)] int id, CancellationToken ct = default)
    {
        var user = await database.Users.GetValidUser(id, ct: ct);
        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        var (_, totalFriends) = await database.Users.Relationship.GetUserFriends(id, ct: ct);
        var (_, totalFollowers) = await database.Users.Relationship.GetUserFollowers(id, ct: ct);

        return Ok(new UserRelationsCountersResponse(totalFollowers, totalFriends));
    }

    [HttpPost]
    [Authorize]
    [Route("{id:int}/friend/status")]
    [EndpointDescription("Change friendship status with user")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EditFriendStatus([Range(1, int.MaxValue)] int id, [FromBody] EditFriendshipStatusRequest request)
    {
        var user = HttpContext.GetCurrentUserOrThrow();

        var relationship = await database.Users.Relationship.GetUserRelationship(user.Id, id);
        if (relationship == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

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
                return Problem($"Invalid action parameter. Use any of: {Enum.GetNames(typeof(UpdateFriendshipStatusAction)).Aggregate((x, y) => x + "," + y)}", statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await database.Users.Relationship.UpdateUserRelationship(relationship);
        if (result.IsFailure)
            return Problem(title: result.Error, statusCode: StatusCodes.Status400BadRequest);

        return new OkResult();
    }

    [HttpGet]
    [Route("{id:int}/medals")]
    [EndpointDescription("Get user medals")]
    [ProducesResponseType(typeof(MedalsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserMedals([Range(1, int.MaxValue)] int id,
        [Required] [FromQuery(Name = "mode")] GameMode mode, CancellationToken ct = default)
    {
        var userMedals = await database.Users.Medals.GetUserMedals(id, mode, ct: ct);
        var modeMedals = await database.Medals.GetMedals(mode, ct: ct);

        return Ok(new MedalsResponse(userMedals, modeMedals));
    }

    [HttpGet]
    [Route("{id:int}/grades")]
    [EndpointDescription("Get user grades")]
    [ProducesResponseType(typeof(GradesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserGrades([Range(1, int.MaxValue)] int id,
        [Required] [FromQuery(Name = "mode")] GameMode mode, CancellationToken ct = default)
    {
        var userGrades = await database.Users.Grades.GetUserGrades(id, mode, ct);

        if (userGrades is null)
            return Problem(ApiErrorResponse.Detail.UserGradesNotFound, statusCode: StatusCodes.Status404NotFound);

        var user = await database.Users.GetUser(userGrades.UserId, ct: ct);

        if (user == null) return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        if (user.IsRestricted())
            return Problem(ApiErrorResponse.Detail.UserIsRestricted, statusCode: StatusCodes.Status404NotFound);

        return Ok(new GradesResponse(userGrades));
    }

    [HttpGet]
    [Route("{id:int}/metadata")]
    [EndpointDescription("Get user metadata")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UserMetadataResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserMetadata([Range(1, int.MaxValue)] int id, CancellationToken ct = default)
    {
        var userMetadata = await database.Users.Metadata.GetUserMetadata(id, ct);

        if (userMetadata is null)
            return Problem(ApiErrorResponse.Detail.UserMetadataNotFound, statusCode: StatusCodes.Status404NotFound);

        var user = await database.Users.GetUser(userMetadata.UserId, ct: ct);

        if (user == null) return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        if (user.IsRestricted())
            return Problem(ApiErrorResponse.Detail.UserIsRestricted, statusCode: StatusCodes.Status404NotFound);

        return Ok(new UserMetadataResponse(userMetadata));
    }

    [HttpPost]
    [Authorize]
    [Route("edit/metadata")]
    [EndpointDescription("Update self metadata")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditSelfUserMetadata([FromBody] EditUserMetadataRequest request, CancellationToken ct = default)
    {
        var user = HttpContext.GetCurrentUserOrThrow();

        var userMetadata = await database.Users.Metadata.GetUserMetadata(user.Id, ct);

        if (userMetadata is null)
            return Problem(ApiErrorResponse.Detail.UserMetadataNotFound, statusCode: StatusCodes.Status404NotFound);

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
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetAvatar()
    {
        var user = HttpContext.GetCurrentUserOrThrow();

        if (Request.HasFormContentType == false)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeAvatar, detail: ApiErrorResponse.Detail.InvalidContentType, statusCode: StatusCodes.Status400BadRequest);

        if (Request.Form.Files.Count == 0)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeAvatar, detail: ApiErrorResponse.Detail.NoFilesWereUploaded, statusCode: StatusCodes.Status400BadRequest);

        var file = Request.Form.Files[0];
        await using var stream = file.OpenReadStream();

        var (isSet, error) = await assetService.SetAvatar(user.Id, stream);

        if (!isSet || error != null)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.AvatarUpload, null, error);
            return Problem(title: ApiErrorResponse.Title.UnableToChangeAvatar, detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        return new OkResult();
    }

    [HttpPost(RequestType.BannerUpload)]
    [Authorize]
    [EndpointDescription("Upload new banner")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetBanner()
    {
        var user = HttpContext.GetCurrentUserOrThrow();

        if (Request.HasFormContentType == false)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeBanner, detail: ApiErrorResponse.Detail.InvalidContentType, statusCode: StatusCodes.Status400BadRequest);

        if (Request.Form.Files.Count == 0)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeBanner, detail: ApiErrorResponse.Detail.NoFilesWereUploaded, statusCode: StatusCodes.Status400BadRequest);

        var file = Request.Form.Files[0];
        await using var stream = file.OpenReadStream();

        var (isSet, error) = await assetService.SetBanner(user.Id, stream);

        if (!isSet || error != null)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.BannerUpload, null, error);
            return Problem(title: ApiErrorResponse.Title.UnableToChangeBanner, detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        return new OkResult();
    }

    [HttpPost(RequestType.PasswordChange)]
    [Authorize]
    [EndpointDescription("Change current password")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = HttpContext.GetCurrentUserOrThrow();

        var userByCurrentPassword = await database.Users.GetUser(passhash: request.CurrentPassword.GetPassHash(), username: user.Username);

        if (userByCurrentPassword == null)
            return Problem(title: ApiErrorResponse.Title.UnableToChangePassword, detail: ApiErrorResponse.Detail.InvalidCurrentPasswordProvided, statusCode: StatusCodes.Status400BadRequest);

        var (isPasswordValid, error) = request.NewPassword.IsValidPassword();

        if (!isPasswordValid)
            return Problem(title: ApiErrorResponse.Title.UnableToChangePassword, detail: error, statusCode: StatusCodes.Status400BadRequest);

        user.Passhash = request.NewPassword.GetPassHash();

        await database.Users.UpdateUser(user);

        var ip = RegionService.GetUserIpAddress(Request);
        await database.Events.Users.AddUserChangePasswordEvent(user.Id, ip.ToString(), request.CurrentPassword.GetPassHash(), request.NewPassword.GetPassHash());

        return new OkResult();
    }

    [HttpPost(RequestType.UsernameChange)]
    [Authorize]
    [EndpointDescription("Change current username")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeUsername([FromBody] UsernameChangeRequest request)
    {
        var user = HttpContext.GetCurrentUserOrThrow();

        var (isUsernameValid, error) = request.NewUsername.IsValidUsername();
        if (!isUsernameValid)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeUsername, detail: error, statusCode: StatusCodes.Status400BadRequest);

        var lastUsernameChange = await database.Events.Users.GetLastUsernameChangeEvent(user.Id);
        if (lastUsernameChange != null && lastUsernameChange.Time.AddDays(Configuration.UsernameChangeCooldownInDays) > DateTime.UtcNow)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeUsername, detail: ApiErrorResponse.Detail.ChangeUsernameOnCooldown(lastUsernameChange.Time.AddDays(Configuration.UsernameChangeCooldownInDays)), statusCode: StatusCodes.Status400BadRequest);

        var foundUserByUsername = await database.Users.GetUser(username: request.NewUsername);
        if (foundUserByUsername != null && foundUserByUsername.IsActive())
            return Problem(title: ApiErrorResponse.Title.UnableToChangeUsername, detail: ApiErrorResponse.Detail.UsernameAlreadyTaken, statusCode: StatusCodes.Status400BadRequest);

        var transactionResult = await database.CommitAsTransactionAsync(async () =>
        {
            if (foundUserByUsername != null)
            {
                var updateFoundUserUsernameResult = await database.Users.UpdateUserUsername(
                    foundUserByUsername,
                    foundUserByUsername.Username,
                    foundUserByUsername.Username.SetUsernameAsOld());

                if (updateFoundUserUsernameResult.IsFailure)
                    throw new ApplicationException("Unexpected error occurred while trying to prepare for changing your username.");
            }

            var oldUsername = user.Username;
            user.Username = request.NewUsername;

            var ip = RegionService.GetUserIpAddress(Request);
            var updateUserUsernameResult = await database.Users.UpdateUserUsername(user, oldUsername, request.NewUsername, null, ip.ToString());
            if (updateUserUsernameResult.IsFailure)
                throw new ApplicationException("Unexpected error occurred while trying to change your username. Sorry!");
        });

        if (transactionResult.IsFailure)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeUsername, detail: transactionResult.Error);

        return new OkResult();
    }

    [HttpPost(RequestType.CountryChange)]
    [Authorize]
    [EndpointDescription("Change current country")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeCountry([FromBody] CountryChangeRequest request)
    {
        if (request.NewCountry == CountryCode.XX)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeCountry, detail: ApiErrorResponse.Detail.CantChangeCountryToUnknown, statusCode: StatusCodes.Status400BadRequest);

        var user = HttpContext.GetCurrentUserOrThrow();

        if (user.Country == request.NewCountry)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeCountry, detail: ApiErrorResponse.Detail.CantChangeCountryToTheSameOne, statusCode: StatusCodes.Status400BadRequest);

        var lastUserCountryChange = await database.Events.Users.GetLastUserCountryChangeEvent(user.Id);

        if (lastUserCountryChange?.Time.AddDays(Configuration.CountryChangeCooldownInDays) > DateTime.UtcNow)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeCountry, detail: ApiErrorResponse.Detail.ChangeCountryOnCooldown(lastUserCountryChange.Time.AddDays(Configuration.CountryChangeCooldownInDays)), statusCode: StatusCodes.Status400BadRequest);

        var ip = RegionService.GetUserIpAddress(Request);

        await database.Users.UpdateUserCountry(user, user.Country, request.NewCountry, user.Id, ip.ToString());

        return Ok();
    }
}