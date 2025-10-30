using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Sunrise.Tests.Utils;

public static partial class FolderUtil
{
    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    public static void Copy(string sourceDir, string entryRoot, bool withFiles = true, bool overwrite = true)
    {
        if (withFiles)
        {
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var dest = Path.Combine(entryRoot, Path.GetFileName(file));
                if (File.Exists(dest) && overwrite)
                    File.Delete(dest);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (!CreateHardLinkW(dest, file, IntPtr.Zero))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create hard link.");
                    }
                }
                else
                {
                    File.Copy(file, dest);
                }
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