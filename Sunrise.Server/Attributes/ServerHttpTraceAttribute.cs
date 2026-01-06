using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Sunrise.API.Attributes;
using Sunrise.API.Extensions;
using Sunrise.Shared.Application;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ServerHttpTraceAttribute : ApiHttpTraceAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        base.OnActionExecuting(context);

        var activity = Activity.Current;

        if (activity == null)
        {
            return;
        }

        var osuToken = context.HttpContext.Request.Headers["osu-token"].ToString();

        if (string.IsNullOrEmpty(osuToken))
        {
            return;
        }

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        if (sessions.TryGetSession(out var session, osuToken) && session != null)
        {
            activity.SetActivitySessionTags(session);
        }
    }
}