using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Sunrise.Server.Tests.Utils;

namespace Sunrise.Server.Tests;

internal class SunriseServerFactory : WebApplicationFactory<Program>
{
    private void ConfigureCurrentDirectory()
    {
        if (Directory.GetCurrentDirectory().Contains("bin"))    
            Directory.SetCurrentDirectory(Path.Combine(Directory.GetCurrentDirectory(), "../../../"));
    }

    private static void ConfigureDataFolder()
    {
        var dataTestFolder = Path.Combine(Directory.GetCurrentDirectory(), "../Data_Tests");
        if (!Directory.Exists(dataTestFolder))
            throw new DirectoryNotFoundException($"Test data folder not found: {dataTestFolder}");

        var dataFolder = Path.Combine(Directory.GetCurrentDirectory(), "Data");

        if (Directory.Exists(dataFolder)) Directory.Delete(dataFolder, true);

        Directory.CreateDirectory(dataFolder);

        FolderUtil.CopyFiles(dataTestFolder, dataFolder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Tests");

        ConfigureCurrentDirectory();
        ConfigureDataFolder();
    }
    
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing) return;

        if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Data")))
            Directory.Delete(Path.Combine(Directory.GetCurrentDirectory(), "Data"), true);
    }
}