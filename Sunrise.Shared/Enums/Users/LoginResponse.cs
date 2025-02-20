namespace Sunrise.Shared.Enums.Users;

public enum LoginResponse
{
    Success = 0,
    InvalidCredentials = -1,
    OutdatedClient = -2,
    UserBanned = -3,
    Multiaccount = -4,
    ServerError = -5,
    CuttingEdgeMultiplayer = -6,
    PasswordReset = -7,
    VerificationRequired = -8
}