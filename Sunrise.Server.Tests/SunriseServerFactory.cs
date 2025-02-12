using Microsoft.AspNetCore.Mvc.Testing;

namespace Sunrise.Server.Tests;

internal class SunriseServerFactory : WebApplicationFactory<Program>
{
    public override async ValueTask DisposeAsync()
    {
        foreach (var factory in Factories)
        {
            await factory.DisposeAsync().ConfigureAwait(false);
        }

        // We don't call base.DisposeAsync() to not dispose hangfire in memory database
        // ! I'm not sure if this is a good idea, but I 'think' it's fine for now
    }
}