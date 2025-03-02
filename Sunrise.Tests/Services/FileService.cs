namespace Sunrise.Tests.Services;

public class FileSizeFilter
{
    public long? MaxSize { get; set; }
    public long? MinSize { get; set; }
}

public class FileService
{
    private static readonly string ResourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "Sunrise.Tests/Resources");

    public string GetRandomFilePath()
    {
        var files = GetAllFilesRecursively(ResourcesPath);
        var random = new Random();
        return files[random.Next(files.Length)];
    }

    public string GetRandomFilePath(string extension, FileSizeFilter? fileSize = null)
    {
        var files = GetAllFilesRecursively(ResourcesPath, $"*.{extension}", fileSize);
        var random = new Random();
        return files[random.Next(files.Length)];
    }

    public string? GetFileByName(string fileName)
    {
        var files = GetAllFilesRecursively(ResourcesPath, fileName);
        return files.FirstOrDefault();
    }

    public string[] GetAllFilesRecursively(string path, string searchPattern = "*", FileSizeFilter? fileSize = null)
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