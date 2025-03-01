using Sunrise.Tests.Manager;

namespace Sunrise.Tests.Abstracts;

public class BaseTest : EnvironmentFixture, IDisposable
{
    protected readonly EnvironmentVariableManager EnvManager = new();

    public new void Dispose()
    {
        EnvManager.Dispose();
        GC.SuppressFinalize(this);
        base.Dispose();
    }
}