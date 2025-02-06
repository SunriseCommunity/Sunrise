
namespace Sunrise.Server.Tests.Core.Utils;

public class FileSizeFilter
{
    public long? MaxSize { get; set; }
    public long? MinSize { get; set; }
}

public static class FileMockUtil
{
    private static readonly string ResoursesPath = Path.Combine(Directory.GetCurrentDirectory(), "Sunrise.Server.Tests/Core/Resources");
    
    public static string GetRandomFilePath()
    {
        var files = GetAllFilesRecursively(ResoursesPath);
        var random = new Random();
        return files[random.Next(files.Length)];
    }
    
    public static string GetRandomFilePath(string extension, FileSizeFilter? fileSize = null)
    {
        var files = GetAllFilesRecursively(ResoursesPath, $"*.{extension}", fileSize);
        var random = new Random();
        return files[random.Next(files.Length)];
    }

    public static string[] GetAllFilesRecursively(string path, string searchPattern = "*", FileSizeFilter? fileSize = null)
    {
        var files = Directory.GetFiles(path, searchPattern, SearchOption.AllDirectories);
        
        if (fileSize == null)
            return files;

        if (fileSize.MaxSize != null)
            files = files.Where(f => new FileInfo(f).Length <= fileSize.MaxSize).ToArray();
            
        if (fileSize.MinSize != null)
            files = files.Where(f => new FileInfo(f).Length >= fileSize.MinSize).ToArray();

        return files;
    }
}