using Sunrise.Shared.Database.Models.User;

namespace Sunrise.Shared.Types.Interfaces;

public interface IBaseSession
{
    // Properties
    User User { get; }
    string Token { get; }

    // Methods
    bool IsRateLimited();
    int GetRemainingCalls();
}