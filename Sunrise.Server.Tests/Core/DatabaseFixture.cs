using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Shared.Application;

namespace Sunrise.Server.Tests.Core;

public class DatabaseFixture : EnvironmentFixture, IDisposable
{
    public new void Dispose()
    {
        if (!Configuration.DataPath.IsDevelopmentFile())
            throw new InvalidOperationException("Data path is not a development directory. Are you trying to delete production data?");

        if (Directory.Exists(Configuration.DataPath))
            Directory.Delete(Path.Combine(Configuration.DataPath), true);

        base.Dispose();
        GC.SuppressFinalize(this);
    }
}