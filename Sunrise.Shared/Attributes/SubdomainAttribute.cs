using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sunrise.Shared.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class SubdomainAttribute(params string[] allowedSubdomains) : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var subdomain = context.HttpContext.Request.Host.Host.Split('.')[0];

        if (!allowedSubdomains.Contains(subdomain))
        {
            context.Result = new JsonResult(new
            {
                error = "No data exists for the requested endpoint"
            })
            {
                StatusCode = 400
            };
        }

        base.OnActionExecuting(context);
    }
}