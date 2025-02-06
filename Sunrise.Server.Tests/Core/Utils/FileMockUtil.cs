
namespace Sunrise.Server.Tests.Core.Utils;

public static class FileMockUtil
{
    private static readonly string ResoursesPath = Path.Combine(Directory.GetCurrentDirectory(), "Sunrise.Server.Tests/Core/Resources");
    
    public static string GetRandomFilePath()
    {
        var files = Directory.GetFiles(ResoursesPath);
        var random = new Random();
        return files[random.Next(files.Length)];
    }
    
    public static string GetRandomImageFilePath(string extension)
    {
        var files = Directory.GetFiles(Path.Combine( ResoursesPath, "Images"), $"*.{extension}");
        var random = new Random();
        return files[random.Next(files.Length)];
    }
}