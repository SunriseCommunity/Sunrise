using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Sunrise.Server.Tests.Core;

namespace Sunrise.Server.Tests;

internal class SunriseServerFactory : WebApplicationFactory<Program>, IClassFixture<EnvironmentFixture>
{

}