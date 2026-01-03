using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Sunrise.API.Extensions;

namespace Sunrise.API.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiHttpTraceAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var activity = Activity.Current;

        if (activity == null)
        {
            return;
        }

        var currentSession = context.HttpContext.GetCurrentSession();
        activity.SetActivitySessionTags(currentSession);

        var currentUser = context.HttpContext.GetCurrentUser();

        if (currentUser != null)
        {
            activity.SetActivityUserTags(currentUser);
        }

    }

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        var activity = Activity.Current;
        if (activity == null) return;

        if (context.Exception != null)
        {
            activity.SetStatus(ActivityStatusCode.Error, context.Exception.Message);
            activity.AddException(context.Exception);
        }
    }
}