using Sunrise.Server.Application;

namespace Sunrise.Server.Tests.Core;

public class DatabaseFixture : IClassFixture<EnvironmentFixture>, IDisposable
{
    public new void Dispose() 
    { 
        if (Directory.Exists(Configuration.DataPath))
            Directory.Delete(Path.Combine(Configuration.DataPath), true);
        
        GC.SuppressFinalize(this);
    }
}