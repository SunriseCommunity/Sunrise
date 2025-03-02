using Sunrise.Tests.Manager;

namespace Sunrise.Tests;

public class EnvironmentFixture : IDisposable
{
    private readonly EnvironmentVariableManager _envManager = new();

    public EnvironmentFixture()
    {
        _envManager.Set("ASPNETCORE_ENVIRONMENT", "Tests");
        ConfigureCurrentDirectory();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static void ConfigureCurrentDirectory()
    {
        if (Directory.GetCurrentDirectory().Contains("bin"))
            Directory.SetCurrentDirectory(Path.Combine(Directory.GetCurrentDirectory(), "../../../../"));
    }
}