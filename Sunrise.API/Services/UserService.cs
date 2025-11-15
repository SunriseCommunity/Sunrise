using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Enums;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Helpers;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Keys;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.API.Services;

public class UserService(
    DatabaseService database,
    AssetService assetService)
{
    public async Task<IActionResult> UpdateUserMetadata(
        UserEventAction eventAction,
        EditUserMetadataRequest request,
        CancellationToken ct = default)
    {
        var userMetadata = await database.Users.Metadata.GetUserMetadata(eventAction.TargetUserId, ct);

        if (userMetadata is null)
            return new ObjectResult(new ProblemDetails
            {
                Detail = ApiErrorResponse.Detail.UserMetadataNotFound,
                Status = StatusCodes.Status404NotFound
            })
            {
                StatusCode = StatusCodes.Status404NotFound
            };

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

        // TODO: Add event logging 

        return new OkResult();
    }

    public async Task<IActionResult> UpdateUserPrivilege(
        UserEventAction eventAction,
        EditUserPrivilegeRequest request,
        CancellationToken ct = default)
    {
        var user = await database.Users.GetUser(eventAction.TargetUserId, ct: ct);

        if (user is null)
            return new ObjectResult(new ProblemDetails
            {
                Detail = ApiErrorResponse.Detail.UserNotFound,
                Status = StatusCodes.Status404NotFound
            })
            {
                StatusCode = StatusCodes.Status404NotFound
            };


        var privilegeEnum = JsonStringFlagEnumHelper.CombineFlags(request.Privilege);

        var updatedPrivileges = user.Privilege ^ privilegeEnum;

        if (eventAction.ExecutorUser.Privilege.GetHighestPrivilege() <= updatedPrivileges.GetHighestPrivilege())
            return new ObjectResult(new ProblemDetails
            {
                Detail = ApiErrorResponse.Detail.InsufficientPrivileges,
                Status = StatusCodes.Status403Forbidden
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };

        user.Privilege = privilegeEnum;

        await database.Users.UpdateUser(user);

        // TODO: Add event logging 

        return new OkResult();
    }

    public async Task<IActionResult> SetUserAvatar(
        UserEventAction eventAction,
        IFormFile file)
    {
        await using var stream = file.OpenReadStream();

        var oldAvatarHash = (await database.Users.Files.GetAvatar(eventAction.TargetUserId))?.GetHashSHA1() ?? string.Empty;

        var (isSet, error) = await assetService.SetAvatar(eventAction.TargetUserId, stream);

        if (!isSet || error != null)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.AvatarUpload, null, error);
            return new ObjectResult(new ProblemDetails
            {
                Title = ApiErrorResponse.Title.UnableToChangeAvatar,
                Detail = error,
                Status = StatusCodes.Status400BadRequest
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var newAvatarHash = (await database.Users.Files.GetAvatar(eventAction.TargetUserId))?.GetHashSHA1() ?? string.Empty;

        await database.Events.Users.AddUserChangeAvatarEvent(
            eventAction,
            oldAvatarHash,
            newAvatarHash
        );

        return new OkResult();
    }

    public async Task<IActionResult> SetUserBanner(
        UserEventAction eventAction,
        IFormFile file)
    {
        await using var stream = file.OpenReadStream();

        var oldBannerHash = (await database.Users.Files.GetBanner(eventAction.TargetUserId))?.GetHashSHA1() ?? string.Empty;

        var (isSet, error) = await assetService.SetBanner(eventAction.TargetUserId, stream);

        if (!isSet || error != null)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.BannerUpload, null, error);
            return new ObjectResult(new ProblemDetails
            {
                Title = ApiErrorResponse.Title.UnableToChangeBanner,
                Detail = error,
                Status = StatusCodes.Status400BadRequest
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var newBannerHash = (await database.Users.Files.GetBanner(eventAction.TargetUserId))?.GetHashSHA1() ?? string.Empty;

        await database.Events.Users.AddUserChangeBannerEvent(
            eventAction,
            oldBannerHash,
            newBannerHash
        );

        return new OkResult();
    }

    public async Task<IActionResult> ResetUserPassword(
        UserEventAction eventAction,
        string newPassword)
    {
        var user = eventAction.TargetUser ?? await database.Users.GetUser(eventAction.TargetUserId);
        if (user == null)
            return new ObjectResult(new ProblemDetails
            {
                Detail = ApiErrorResponse.Detail.UserNotFound,
                Status = StatusCodes.Status404NotFound
            })
            {
                StatusCode = StatusCodes.Status404NotFound
            };

        var (isPasswordValid, error) = newPassword.IsValidPassword();

        if (!isPasswordValid)
            return new ObjectResult(new ProblemDetails
            {
                Title = ApiErrorResponse.Title.UnableToChangePassword,
                Detail = error,
                Status = StatusCodes.Status400BadRequest
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

        var oldPasshash = user.Passhash;
        user.Passhash = newPassword.GetPassHash();

        await database.Users.UpdateUser(user);

        await database.Events.Users.AddUserChangePasswordEvent(
            eventAction,
            oldPasshash,
            user.Passhash);

        return new OkResult();
    }

    public async Task<IActionResult> ChangeUserUsername(
        UserEventAction eventAction,
        string newUsername,
        bool skipCooldownCheck = false)
    {
        var user = eventAction.TargetUser ?? await database.Users.GetUser(eventAction.TargetUserId);
        if (user == null)
            return new ObjectResult(new ProblemDetails
            {
                Detail = ApiErrorResponse.Detail.UserNotFound,
                Status = StatusCodes.Status404NotFound
            })
            {
                StatusCode = StatusCodes.Status404NotFound
            };

        var (isUsernameValid, error) = newUsername.IsValidUsername();
        if (!isUsernameValid)
            return new ObjectResult(new ProblemDetails
            {
                Title = ApiErrorResponse.Title.UnableToChangeUsername,
                Detail = error,
                Status = StatusCodes.Status400BadRequest
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

        if (!skipCooldownCheck)
        {
            var lastUsernameChange = await database.Events.Users.GetLastUsernameChangeEvent(user.Id);
            if (lastUsernameChange != null &&
                lastUsernameChange.Time.AddDays(Configuration.UsernameChangeCooldownInDays) > DateTime.UtcNow)
                return new ObjectResult(new ProblemDetails
                {
                    Title = ApiErrorResponse.Title.UnableToChangeUsername,
                    Detail = ApiErrorResponse.Detail.ChangeUsernameOnCooldown(
                        lastUsernameChange.Time.AddDays(Configuration.UsernameChangeCooldownInDays)),
                    Status = StatusCodes.Status400BadRequest
                })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
        }

        var foundUserByUsername = await database.Users.GetUser(username: newUsername);
        if (foundUserByUsername != null && foundUserByUsername.IsActive())
            return new ObjectResult(new ProblemDetails
            {
                Title = ApiErrorResponse.Title.UnableToChangeUsername,
                Detail = ApiErrorResponse.Detail.UsernameAlreadyTaken,
                Status = StatusCodes.Status400BadRequest
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

        var transactionResult = await database.CommitAsTransactionAsync(async () =>
        {
            if (foundUserByUsername != null)
            {
                var updateFoundUserUsernameResult = await database.Users.UpdateUserUsername(
                    new UserEventAction(eventAction.ExecutorUser, eventAction.ExecutorIp, foundUserByUsername.Id, foundUserByUsername),
                    foundUserByUsername.Username,
                    foundUserByUsername.Username.SetUsernameAsOld());

                if (updateFoundUserUsernameResult.IsFailure)
                    throw new ApplicationException("Unexpected error occurred while trying to prepare for changing username.");
            }

            var oldUsername = user.Username;
            user.Username = newUsername;

            var updateUserUsernameResult = await database.Users.UpdateUserUsername(
                new UserEventAction(eventAction.ExecutorUser, eventAction.ExecutorIp, user.Id, user),
                oldUsername,
                newUsername);
            if (updateUserUsernameResult.IsFailure)
                throw new ApplicationException("Unexpected error occurred while trying to change username.");
        });

        if (transactionResult.IsFailure)
            return new ObjectResult(new ProblemDetails
            {
                Title = ApiErrorResponse.Title.UnableToChangeUsername,
                Detail = transactionResult.Error,
                Status = StatusCodes.Status400BadRequest
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

        return new OkResult();
    }

    public async Task<IActionResult> ChangeUserCountry(
        UserEventAction eventAction,
        CountryCode newCountry,
        bool skipCooldownCheck = false)
    {
        if (newCountry == CountryCode.XX)
            return new ObjectResult(new ProblemDetails
            {
                Title = ApiErrorResponse.Title.UnableToChangeCountry,
                Detail = ApiErrorResponse.Detail.CantChangeCountryToUnknown,
                Status = StatusCodes.Status400BadRequest
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

        var user = eventAction.TargetUser ?? await database.Users.GetUser(eventAction.TargetUserId);
        if (user == null)
            return new ObjectResult(new ProblemDetails
            {
                Detail = ApiErrorResponse.Detail.UserNotFound,
                Status = StatusCodes.Status404NotFound
            })
            {
                StatusCode = StatusCodes.Status404NotFound
            };

        if (user.Country == newCountry)
            return new ObjectResult(new ProblemDetails
            {
                Title = ApiErrorResponse.Title.UnableToChangeCountry,
                Detail = ApiErrorResponse.Detail.CantChangeCountryToTheSameOne,
                Status = StatusCodes.Status400BadRequest
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

        if (!skipCooldownCheck)
        {
            var lastUserCountryChange = await database.Events.Users.GetLastUserCountryChangeEvent(user.Id);

            if (lastUserCountryChange?.Time.AddDays(Configuration.CountryChangeCooldownInDays) > DateTime.UtcNow)
                return new ObjectResult(new ProblemDetails
                {
                    Title = ApiErrorResponse.Title.UnableToChangeCountry,
                    Detail = ApiErrorResponse.Detail.ChangeCountryOnCooldown(
                        lastUserCountryChange.Time.AddDays(Configuration.CountryChangeCooldownInDays)),
                    Status = StatusCodes.Status400BadRequest
                })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
        }

        await database.Users.UpdateUserCountry(
            eventAction,
            user.Country,
            newCountry
        );

        return new OkResult();
    }

    public async Task<IActionResult> UpdateUserDescription(UserEventAction eventAction, string description)
    {
        var user = eventAction.TargetUser ?? await database.Users.GetUser(eventAction.TargetUserId);
        if (user == null)
            return
                new ObjectResult(new ProblemDetails
                {
                    Detail = ApiErrorResponse.Detail.UserNotFound,
                    Status = StatusCodes.Status404NotFound
                })
                {
                    StatusCode = StatusCodes.Status404NotFound
                };

        user.Description = description;
        await database.Users.UpdateUser(user);

        // TODO: Add event logging about who changed the description

        return new OkResult();
    }

    public async Task<IActionResult> UpdateUserDefaultGameMode(UserEventAction eventAction, GameMode defaultGameMode)
    {
        var user = eventAction.TargetUser ?? await database.Users.GetUser(eventAction.TargetUserId);
        if (user == null)
            return new ObjectResult(new ProblemDetails
            {
                Detail = ApiErrorResponse.Detail.UserNotFound,
                Status = StatusCodes.Status404NotFound
            })
            {
                StatusCode = StatusCodes.Status404NotFound
            };

        user.DefaultGameMode = defaultGameMode;
        await database.Users.UpdateUser(user);

        // TODO: Add event logging about who changed the default game mode

        return new OkResult();
    }

    public async Task<IActionResult> ChangeUserPassword(
        UserEventAction eventAction,
        string currentPassword,
        string newPassword)
    {
        var user = eventAction.TargetUser ?? await database.Users.GetUser(eventAction.TargetUserId);
        if (user == null)
            return new ObjectResult(new ProblemDetails
            {
                Detail = ApiErrorResponse.Detail.UserNotFound,
                Status = StatusCodes.Status404NotFound
            })
            {
                StatusCode = StatusCodes.Status404NotFound
            };

        var userByCurrentPassword = await database.Users.GetUser(passhash: currentPassword.GetPassHash(), username: user.Username);
        if (userByCurrentPassword == null)
            return new ObjectResult(new ProblemDetails
            {
                Title = ApiErrorResponse.Title.UnableToChangePassword,
                Detail = ApiErrorResponse.Detail.InvalidCurrentPasswordProvided,
                Status = StatusCodes.Status400BadRequest
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

        var (isPasswordValid, error) = newPassword.IsValidPassword();
        if (!isPasswordValid)
            return new ObjectResult(new ProblemDetails
            {
                Title = ApiErrorResponse.Title.UnableToChangePassword,
                Detail = error,
                Status = StatusCodes.Status400BadRequest
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

        var oldPasshash = user.Passhash;
        user.Passhash = newPassword.GetPassHash();
        await database.Users.UpdateUser(user);

        await database.Events.Users.AddUserChangePasswordEvent(
            eventAction,
            oldPasshash,
            user.Passhash);

        return new OkResult();
    }

    public async Task<IActionResult> UpdateFriendshipStatus(
        UserEventAction eventAction,
        int targetFriendshipUserId,
        UpdateFriendshipStatusAction action)
    {
        var relationship = await database.Users.Relationship.GetUserRelationship(eventAction.TargetUserId, targetFriendshipUserId);
        if (relationship == null)
            return new ObjectResult(new ProblemDetails
            {
                Detail = ApiErrorResponse.Detail.UserNotFound,
                Status = StatusCodes.Status404NotFound
            })
            {
                StatusCode = StatusCodes.Status404NotFound
            };

        switch (action)
        {
            case UpdateFriendshipStatusAction.Add:
                relationship.Relation = UserRelation.Friend;
                break;
            case UpdateFriendshipStatusAction.Remove:
                relationship.Relation = UserRelation.None;
                break;
            default:
                return new ObjectResult(new ProblemDetails
                {
                    Title = "Invalid action",
                    Detail = $"Invalid action parameter. Use any of: {Enum.GetNames(typeof(UpdateFriendshipStatusAction)).Aggregate((x, y) => x + "," + y)}",
                    Status = StatusCodes.Status400BadRequest
                })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
        }

        var result = await database.Users.Relationship.UpdateUserRelationship(relationship);
        if (result.IsFailure)
            return new ObjectResult(new ProblemDetails
            {
                Title = result.Error,
                Status = StatusCodes.Status400BadRequest
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

        // TODO: Add event logging about who changed the friendship status

        return new OkResult();
    }

    public static List<UserBadge> GetUserBadges(User user)
    {
        var badges = new List<UserBadge>();

        if (user.Privilege.HasFlag(UserPrivilege.Developer))
            badges.Add(UserBadge.Developer);

        if (user.Privilege.HasFlag(UserPrivilege.Admin))
            badges.Add(UserBadge.Admin);

        if (user.Privilege.HasFlag(UserPrivilege.Bat))
            badges.Add(UserBadge.Bat);

        if (user.IsUserSunriseBot())
            badges.Add(UserBadge.Bot);

        if (user.Privilege.HasFlag(UserPrivilege.Supporter))
            badges.Add(UserBadge.Supporter);

        return badges;
    }
}