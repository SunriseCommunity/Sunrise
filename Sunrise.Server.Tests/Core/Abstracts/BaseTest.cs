using Sunrise.Server.Tests.Core.Manager;

namespace Sunrise.Server.Tests.Core.Abstracts;

public class BaseTest : IClassFixture<EnvironmentFixture>, IDisposable
{
    protected readonly EnvironmentVariableManager EnvManager = new();
    
    public void Dispose()
    {
        EnvManager.Dispose();
        GC.SuppressFinalize(this);
    }
}