using Sunrise.Server.Application;

namespace Sunrise.Server.Tests.Core;

public class DatabaseFixture : IDisposable
{
    public DatabaseFixture()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Tests");
        ConfigureCurrentDirectory();
    }
    
    public void Dispose()
    {
       Directory.Delete(Path.Combine(Configuration.DataPath), true);
    }
    
    private static void ConfigureCurrentDirectory()
    {
        if (Directory.GetCurrentDirectory().Contains("bin"))    
            Directory.SetCurrentDirectory(Path.Combine(Directory.GetCurrentDirectory(), "../../../../"));
    }
}