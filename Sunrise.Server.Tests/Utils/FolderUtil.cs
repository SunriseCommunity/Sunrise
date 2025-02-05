namespace Sunrise.Server.Tests.Utils;

public static class FolderUtil
{
    public static void CopyFiles(string sourceDir, string entryRoot)
    {
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(entryRoot, Path.GetFileName(file));
            File.Copy(file, dest);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dest = Path.Combine(entryRoot, Path.GetFileName(dir));
            Directory.CreateDirectory(dest);
            CopyFiles(dir, dest);
        }
    }
}