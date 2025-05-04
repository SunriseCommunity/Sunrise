using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Sunrise.API.Extensions;
using Sunrise.API.Serializable.Response;
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

                if (user.Privilege.HasFlag(requiredPrivilege))
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
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErrorResponse("You can't access this resource."));
            return;
        }

        if (!authorizeResult.Succeeded)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErrorResponse("Please authorize to access this resource."));
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}