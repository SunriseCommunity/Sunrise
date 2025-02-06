using Sunrise.Server.Application;

namespace Sunrise.Server.Tests.Core;

public class EnvironmentFixture : IDisposable
{
    public EnvironmentFixture ()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Tests");
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