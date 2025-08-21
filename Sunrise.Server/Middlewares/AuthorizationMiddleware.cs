using System.Security.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Sunrise.API.Extensions;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.Server.Middlewares;

public class UserPrivilegeRequirement(UserPrivilege privilege) : IAuthorizationRequirement
{
    public UserPrivilege Privilege { get; } = privilege;
}

public class DatabaseAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (context.Resource is not HttpContext httpContext)
            return Task.CompletedTask;

        var user = httpContext.GetCurrentUser();

        if (user == null)
            return Task.CompletedTask;

        foreach (var requirement in context.PendingRequirements)
        {
            if (requirement is UserPrivilegeRequirement privilegeRequirement)
            {
                var requiredPrivilege = privilegeRequirement.Privilege;

                if (user.Privilege.HasFlag(requiredPrivilege) || user.Privilege.HasFlag(UserPrivilege.Developer))
                {
                    context.Succeed(requirement);
                }
            }
        }

        return Task.CompletedTask;
    }
}

public class CustomAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            throw new AuthenticationException("You can't access this resource.");
        }

        if (!authorizeResult.Succeeded)
        {
            throw new UnauthorizedAccessException("Please authorize to access this resource.");
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}