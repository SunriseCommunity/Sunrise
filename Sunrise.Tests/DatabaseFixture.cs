using Sunrise.Shared.Application;
using Sunrise.Tests.Utils;

namespace Sunrise.Tests;

public class FilesystemFixture : EnvironmentFixture, IDisposable
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