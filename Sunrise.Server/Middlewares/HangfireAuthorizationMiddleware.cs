using Hangfire.Dashboard;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Middlewares;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var ip = RegionService.GetUserIpAddress(context.GetHttpContext().Request);

        return ip.IsFromLocalNetwork() ||
               ip.IsFromDocker();
    }
}