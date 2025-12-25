using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;

namespace Sunrise.Shared.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class SubdomainRouteAttribute(string template, params string[] allowedSubdomains) : RouteAttribute(template), IActionConstraint
{
    public bool Accept(ActionConstraintContext context)
    {
        var host = context.RouteContext.HttpContext.Request.Host.Host;
        var parts = host.Split('.');

        if (parts.Length < 2)
            return false;

        var subdomain = parts[0];

        return allowedSubdomains.Contains(subdomain);
    }
}
