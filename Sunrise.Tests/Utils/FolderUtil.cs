namespace Sunrise.Tests.Utils;

public static class FolderUtil
{
    public static void Copy(string sourceDir, string entryRoot, bool withFiles = true, bool overwrite = true)
    {
        if (withFiles)
        {
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var dest = Path.Combine(entryRoot, Path.GetFileName(file));
                if (File.Exists(dest) && overwrite)
                    File.Delete(dest);

                File.Copy(file, dest);
            }
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dest = Path.Combine(entryRoot, Path.GetFileName(dir));
            if (!Directory.Exists(dest))
                Directory.CreateDirectory(dest);

            Copy(dir, dest, withFiles);
        }
    }

    public static bool IsDevelopmentFile(this string path)
    {
        return path.Contains(".tmp", StringComparison.CurrentCultureIgnoreCase);
    }
}