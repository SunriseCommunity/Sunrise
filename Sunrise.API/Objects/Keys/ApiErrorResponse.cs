namespace Sunrise.API.Objects.Keys;

public static class ApiErrorResponse
{
    public static class Title
    {
        public const string ValidationError = "One or more validation errors occurred.";

        public const string UnableToChangeUsername = "Unable to change username.";
        public const string UnableToChangePassword = "Unable to change password.";
        public const string UnableToChangeBanner = "Unable to change banner.";
        public const string UnableToChangeAvatar = "Unable to change avatar.";
        public const string UnableToChangeCountry = "Unable to change country.";

        public const string UnableToRegisterUser = "Unable to register user.";
        public const string UnableToAuthenticate = "Unable to authenticate.";
        public const string UnableToRefreshAuthToken = "Unable to refresh auth token.";
    }

    public static class Detail
    {
        public const string UnknownErrorOccurred = "Unknown error occurred.";

        public const string UserNotFound = "User not found.";
        public const string UserMetadataNotFound = "User metadata not found.";
        public const string UserGradesNotFound = "User grades not found.";
        public const string UserStatsNotFound = "User stats not found.";

        public const string CurrentUserSessionNotFound = "User with current session not found.";
        public const string CantCheckSelfFriendshipStatus = "You can't check your own friendship status.";

        public const string UserIsRestricted = "User is restricted.";

        public const string ScoreNotFound = "Score not found.";
        public const string ReplayNotFound = "Replay not found.";
        public const string BeatmapNotFound = "Beatmap not found.";


        public const string InvalidCredentialsProvided = "Invalid credentials provided.";

        public const string CantChangeCountryToTheSameOne = "You can't change country to the same one.";
        public const string CantChangeCountryToUnknown = "You can't change country to the unknown one.";

        public const string UsernameAlreadyTaken = "Username already taken.";

        public const string InvalidContentType = "Invalid content type.";
        public const string NoFilesWereUploaded = "No files were uploaded.";

        public const string InvalidCurrentPasswordProvided = "Invalid current password provided.";

        public const string YouHaveBeenBanned = "You have been banned. Please contact servers support for unban.";

        public const string AuthorizationFailed = "Authorization failed. Please authorize to access this resource.";

        public static string YourAccountIsRestricted(string? reason)
        {
            var formattedReason = reason != null ? $" Reason: {reason}" : string.Empty;
            return "Your account is restricted." + formattedReason;
        }

        public static string ChangeUsernameOnCooldown(DateTime changePossibleAfterDateTime)
        {
            return $"You'll be able to change your username on {changePossibleAfterDateTime} UTC+0";
        }

        public static string ChangeCountryOnCooldown(DateTime changePossibleAfterDateTime)
        {
            return $"You'll be able to change your country on {changePossibleAfterDateTime} UTC+0";
        }
    }
}