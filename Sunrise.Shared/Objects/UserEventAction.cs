using Sunrise.Shared.Database.Models.Users;

namespace Sunrise.Shared.Objects;

public class UserEventAction(User executorUser, string executorIp, int targetUserId, User? targetUser = null)
{
    public User ExecutorUser { get; } = executorUser;
    public string ExecutorIp { get; } = executorIp;
    public int TargetUserId { get; } = targetUserId;
    public User? TargetUser { get; } = targetUser ?? (executorUser.Id == targetUserId ? executorUser : null);
}