using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sunrise.API.Attributes;
using Sunrise.API.Extensions;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.API.Services;
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
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Serializable.Events;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.API.Controllers;

[ApiController]
[ApiHttpTrace]
[Route("/user")]
[Subdomain("api")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status400BadRequest)]
public class UserController(BeatmapService beatmapService, DatabaseService database, SessionRepository sessions, UserService userService) : ControllerBase
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
    [Authorize("RequireAdmin")]
    [Route("{id:int}/sensitive")]
    [EndpointDescription("Get user sensitive profile")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UserSensitiveResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserSensitive([Range(1, int.MaxValue)] int id, CancellationToken ct = default)
    {
        var isRequestingSelf = id == HttpContext.GetCurrentSession().UserId;
        var user = isRequestingSelf ? HttpContext.GetCurrentUser() : await database.Users.GetUser(id, options: new QueryOptions(true), ct: ct);

        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        return Ok(new UserSensitiveResponse(sessions, user));
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
        var ip = RegionService.GetUserIpAddress(Request);
        return await userService.UpdateUserDescription(new UserEventAction(user, ip.ToString(), user.Id), request.Description);
    }

    [HttpPost]
    [Authorize("RequireAdmin")]
    [Route("{id:int}/edit/description")]
    [EndpointDescription("Update users description")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EditUserDescription(
        [Range(1, int.MaxValue)] int id,
        [FromBody] EditDescriptionRequest request)
    {
        var currentUser = HttpContext.GetCurrentUserOrThrow();
        var user = await database.Users.GetUser(id);
        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        var ip = RegionService.GetUserIpAddress(Request);

        return await userService.UpdateUserDescription(new UserEventAction(currentUser, ip.ToString(), user.Id), request.Description);
    }

    [HttpPost]
    [Authorize]
    [Route("edit/default-gamemode")]
    [EndpointDescription("Update current users default gamemode")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EditUserDefaultGameMode([FromBody] EditDefaultGameModeRequest request)
    {
        var user = HttpContext.GetCurrentUserOrThrow();
        var ip = RegionService.GetUserIpAddress(Request);

        return await userService.UpdateUserDefaultGameMode(new UserEventAction(user, ip.ToString(), user.Id), request.DefaultGameMode);
    }


    [HttpPost]
    [Authorize("RequireAdmin")]
    [Route("{id:int}/edit/restriction")]
    [EndpointDescription("Update users restriction status")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EditUserRestriction(
        [Range(1, int.MaxValue)] int id,
        [FromBody] EditUserRestrictionRequest request)
    {

        var currentUser = HttpContext.GetCurrentUserOrThrow();

        var user = await database.Users.GetUser(id);
        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        var isRestricted = await database.Users.Moderation.IsUserRestricted(user.Id);

        var ip = RegionService.GetUserIpAddress(Request).ToString();

        if (!request.IsRestrict)
        {
            if (!isRestricted)
                return Problem(ApiErrorResponse.Detail.UserAlreadyUnrestricted, statusCode: StatusCodes.Status400BadRequest);

            var unrestrictUserResult = await database.Users.Moderation.UnrestrictPlayer(user.Id, currentUser.Id, ip);
            if (unrestrictUserResult.IsFailure)
                return Problem(unrestrictUserResult.Error, statusCode: StatusCodes.Status500InternalServerError);

            return Ok();
        }

        if (isRestricted)
            return Problem(ApiErrorResponse.Detail.UserAlreadyRestricted, statusCode: StatusCodes.Status400BadRequest);

        if (string.IsNullOrWhiteSpace(request.RestrictionReason))
            return Problem(ApiErrorResponse.Detail.RestrictionReasonMustBeProvided, statusCode: StatusCodes.Status400BadRequest);

        var restrictUserResult = await database.Users.Moderation.RestrictPlayer(user.Id, currentUser.Id, request.RestrictionReason, TimeSpan.FromDays(365 * 10), ip);
        if (restrictUserResult.IsFailure)
            return Problem(restrictUserResult.Error, statusCode: StatusCodes.Status500InternalServerError);

        return Ok();
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
        var (users, _) = await database.Users.GetValidUsersByQueryLike(query,
            new QueryOptions(true, new Pagination(page, limit))
            {
                IgnoreCountQueryIfExists = true
            },
            ct);

        return Ok(users.Select(x => new UserResponse(sessions, x)));
    }

    [HttpGet]
    [Authorize("RequireAdmin")]
    [Route("search/list")]
    [EndpointDescription("Search user by query")]
    [ProducesResponseType(typeof(UsersSensitiveListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchUserSensitivesList(
        [FromQuery(Name = "query")] string? query,
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int limit = 50,
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int page = 1,
        CancellationToken ct = default
    )
    {
        var (users, totalCount) = await database.Users.GetUsersBySensitiveInfoQueryLike(query, new QueryOptions(true, new Pagination(page, limit)), ct);

        return Ok(new UsersSensitiveListResponse(users.Select(x => new UserSensitiveResponse(sessions, x)).ToList(), totalCount));
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
    [Authorize("RequireAdmin")]
    [Route("{id:int}/friends")]
    [EndpointDescription("Get users friends")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FriendsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserFriends(
        [Range(1, int.MaxValue)] int id,
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int limit = 50,
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int page = 1,
        CancellationToken ct = default
    )
    {
        var user = await database.Users.GetUser(id, ct: ct);
        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

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
    [Authorize("RequireAdmin")]
    [Route("{id:int}/followers")]
    [EndpointDescription("Get users followers")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FollowersResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserFollowers(
        [Range(1, int.MaxValue)] int id,
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int limit = 50,
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int page = 1,
        CancellationToken ct = default
    )
    {
        var user = await database.Users.GetUser(id, ct: ct);
        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

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
    [Authorize("RequireAdmin")]
    [Route("{id:int}/events")]
    [EndpointDescription("Get users events")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(EventUsersResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserEvents(
        [Range(1, int.MaxValue)] int id,
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int limit = 50,
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int page = 1,
        [FromQuery(Name = "query")] string? query = null,
        [FromQuery(Name = "types")] List<UserEventType>? userEventType = null,
        CancellationToken ct = default
    )
    {
        return await userService.GetUserEvents(id, page, limit, query, userEventType, ct);
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
        var ip = RegionService.GetUserIpAddress(Request);

        return await userService.UpdateFriendshipStatus(new UserEventAction(user, ip.ToString(), user.Id), id, request.Action);
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

        var currentUser = HttpContext.GetCurrentUser();

        if (user.IsRestricted() && (currentUser == null || !currentUser.Privilege.HasFlag(UserPrivilege.Admin)))
            return Problem(ApiErrorResponse.Detail.UserIsRestricted, statusCode: StatusCodes.Status404NotFound);

        return Ok(new UserMetadataResponse(userMetadata));
    }

    [HttpPost]
    [Authorize("RequireAdmin")]
    [Route("{id:int}/edit/metadata")]
    [EndpointDescription("Update user metadata")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditUserMetadata(
        [Range(1, int.MaxValue)] int id,
        [FromBody] EditUserMetadataRequest request, CancellationToken ct = default)
    {
        var currentUser = HttpContext.GetCurrentUserOrThrow();
        var ip = RegionService.GetUserIpAddress(Request);

        return await userService.UpdateUserMetadata(new UserEventAction(currentUser, ip.ToString(), id), request, ct);
    }

    [HttpPost]
    [Authorize("RequireAdmin")]
    [Route("{id:int}/edit/privilege")]
    [EndpointDescription("Update user privilege")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditUserPrivilege(
        [Range(1, int.MaxValue)] int id,
        [FromBody] EditUserPrivilegeRequest request, CancellationToken ct = default)
    {
        var currentUser = HttpContext.GetCurrentUserOrThrow();
        var ip = RegionService.GetUserIpAddress(Request);

        return await userService.UpdateUserPrivilege(new UserEventAction(currentUser, ip.ToString(), id), request, ct);
    }

    [HttpPost]
    [Authorize]
    [Route("edit/metadata")]
    [EndpointDescription("Update self metadata")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditSelfUserMetadata([FromBody] EditUserMetadataRequest request, CancellationToken ct = default)
    {
        var user = HttpContext.GetCurrentUserOrThrow();
        var ip = RegionService.GetUserIpAddress(Request);

        return await userService.UpdateUserMetadata(new UserEventAction(user, ip.ToString(), user.Id), request, ct);
    }

    [HttpPost(RequestType.UploadUserAvatar)]
    [Authorize("RequireAdmin")]
    [EndpointDescription("Upload new avatar")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetUserAvatar([Range(1, int.MaxValue)] int id)
    {
        var currentUser = HttpContext.GetCurrentUserOrThrow();

        var user = await database.Users.GetUser(id);
        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        var ip = RegionService.GetUserIpAddress(Request);

        if (Request.HasFormContentType == false)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeAvatar, detail: ApiErrorResponse.Detail.InvalidContentType, statusCode: StatusCodes.Status400BadRequest);

        if (Request.Form.Files.Count == 0)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeAvatar, detail: ApiErrorResponse.Detail.NoFilesWereUploaded, statusCode: StatusCodes.Status400BadRequest);

        var file = Request.Form.Files[0];
        return await userService.SetUserAvatar(new UserEventAction(currentUser, ip.ToString(), id, user), file);
    }

    [HttpPost(RequestType.AvatarUpload)]
    [Authorize]
    [EndpointDescription("Upload new avatar")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetAvatar()
    {
        var user = HttpContext.GetCurrentUserOrThrow();
        var ip = RegionService.GetUserIpAddress(Request);

        if (Request.HasFormContentType == false)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeAvatar, detail: ApiErrorResponse.Detail.InvalidContentType, statusCode: StatusCodes.Status400BadRequest);

        if (Request.Form.Files.Count == 0)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeAvatar, detail: ApiErrorResponse.Detail.NoFilesWereUploaded, statusCode: StatusCodes.Status400BadRequest);

        var file = Request.Form.Files[0];
        return await userService.SetUserAvatar(new UserEventAction(user, ip.ToString(), user.Id), file);
    }

    [HttpPost(RequestType.UploadUserBanner)]
    [Authorize("RequireAdmin")]
    [EndpointDescription("Upload user banner")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetUserBanner([Range(1, int.MaxValue)] int id)
    {
        var currentUser = HttpContext.GetCurrentUserOrThrow();

        var user = await database.Users.GetUser(id);
        if (user == null)
            return Problem(ApiErrorResponse.Detail.UserNotFound, statusCode: StatusCodes.Status404NotFound);

        var ip = RegionService.GetUserIpAddress(Request);

        if (Request.HasFormContentType == false)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeBanner, detail: ApiErrorResponse.Detail.InvalidContentType, statusCode: StatusCodes.Status400BadRequest);

        if (Request.Form.Files.Count == 0)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeBanner, detail: ApiErrorResponse.Detail.NoFilesWereUploaded, statusCode: StatusCodes.Status400BadRequest);

        var file = Request.Form.Files[0];
        return await userService.SetUserBanner(new UserEventAction(currentUser, ip.ToString(), id, user), file);
    }

    [HttpPost(RequestType.BannerUpload)]
    [Authorize]
    [EndpointDescription("Upload new banner")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetBanner()
    {
        var user = HttpContext.GetCurrentUserOrThrow();
        var ip = RegionService.GetUserIpAddress(Request);

        if (Request.HasFormContentType == false)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeBanner, detail: ApiErrorResponse.Detail.InvalidContentType, statusCode: StatusCodes.Status400BadRequest);

        if (Request.Form.Files.Count == 0)
            return Problem(title: ApiErrorResponse.Title.UnableToChangeBanner, detail: ApiErrorResponse.Detail.NoFilesWereUploaded, statusCode: StatusCodes.Status400BadRequest);

        var file = Request.Form.Files[0];
        return await userService.SetUserBanner(new UserEventAction(user, ip.ToString(), user.Id), file);
    }

    [HttpPost(RequestType.ChangeUsersPassword)]
    [Authorize("RequireAdmin")]
    [EndpointDescription("Change users current password")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeUsersPassword(
        [Range(1, int.MaxValue)] int id,
        [FromBody] ResetPasswordRequest request)
    {
        var currentUser = HttpContext.GetCurrentUserOrThrow();
        var ip = RegionService.GetUserIpAddress(Request);
        return await userService.ResetUserPassword(new UserEventAction(currentUser, ip.ToString(), id), request.NewPassword);
    }

    [HttpPost(RequestType.PasswordChange)]
    [Authorize]
    [EndpointDescription("Change current password")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = HttpContext.GetCurrentUserOrThrow();
        var ip = RegionService.GetUserIpAddress(Request);
        return await userService.ChangeUserPassword(new UserEventAction(user, ip.ToString(), user.Id), request.CurrentPassword, request.NewPassword);
    }

    [HttpPost(RequestType.ChangeUsersUsername)]
    [Authorize("RequireAdmin")]
    [EndpointDescription("Change users current username")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeUsername(
        [Range(1, int.MaxValue)] int id,
        [FromBody] UsernameChangeRequest request)
    {
        var currentUser = HttpContext.GetCurrentUserOrThrow();
        var ip = RegionService.GetUserIpAddress(Request);
        return await userService.ChangeUserUsername(new UserEventAction(currentUser, ip.ToString(), id), request.NewUsername, true);
    }

    [HttpPost(RequestType.UsernameChange)]
    [Authorize]
    [EndpointDescription("Change current username")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeUsername([FromBody] UsernameChangeRequest request)
    {
        var user = HttpContext.GetCurrentUserOrThrow();
        var ip = RegionService.GetUserIpAddress(Request);
        return await userService.ChangeUserUsername(new UserEventAction(user, ip.ToString(), user.Id), request.NewUsername);
    }

    [HttpPost(RequestType.ChangeUsersCountry)]
    [Authorize("RequireAdmin")]
    [EndpointDescription("Change users current country")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeUsersCountry(
        [Range(1, int.MaxValue)] int id,
        [FromBody] CountryChangeRequest request)
    {
        var currentUser = HttpContext.GetCurrentUserOrThrow();
        var ip = RegionService.GetUserIpAddress(Request);
        return await userService.ChangeUserCountry(new UserEventAction(currentUser, ip.ToString(), id), request.NewCountry, true);
    }

    [HttpPost(RequestType.CountryChange)]
    [Authorize]
    [EndpointDescription("Change current country")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeCountry([FromBody] CountryChangeRequest request)
    {
        var user = HttpContext.GetCurrentUserOrThrow();
        var ip = RegionService.GetUserIpAddress(Request);
        return await userService.ChangeUserCountry(new UserEventAction(user, ip.ToString(), user.Id), request.NewCountry);
    }
}