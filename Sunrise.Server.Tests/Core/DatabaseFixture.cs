using Sunrise.Server.Application;

namespace Sunrise.Server.Tests.Core;

public class DatabaseFixture : EnvironmentFixture, IDisposable
{
    public new void Dispose()
    {
        if (Directory.Exists(Configuration.DataPath))
            Directory.Delete(Path.Combine(Configuration.DataPath), true);

        base.Dispose();
        GC.SuppressFinalize(this);
    }
}