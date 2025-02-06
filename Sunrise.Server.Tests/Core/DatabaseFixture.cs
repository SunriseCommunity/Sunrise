using Sunrise.Server.Application;

namespace Sunrise.Server.Tests.Core;

public class DatabaseFixture : IDisposable, IClassFixture<EnvironmentFixture>
{
    public void Dispose()
    { 
        if (Directory.Exists(Configuration.DataPath))
            Directory.Delete(Path.Combine(Configuration.DataPath), true);
    }
}