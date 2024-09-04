using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace Sunrise.Server.Utils;

public static class ImageTools
{
    private static readonly Dictionary<string, byte[]> ValidImageBytes = new()
    {
        // @formatter:off
        {".bmp", [66, 77]},
        {".gif", [71, 73, 70, 56]},
        {".ico", [0, 0, 1, 0]},
        {".jpg", [255, 216, 255]},
        {".png", [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82]},
        {".tiff", [73, 73, 42, 0]}
        // @formatter:on
    };

    public static byte[] ResizeImage(byte[] bytes, int width, int height)
    {
        using var memoryStream = new MemoryStream(bytes);

        var imageFormat = Image.DetectFormat(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        using var image = Image.Load(memoryStream);
        image.Mutate(x => x.Resize(width, height));

        using var outputStream = new MemoryStream();

        if (imageFormat.Name == "GIF")
        {
            image.Save(outputStream, new GifEncoder());
        }
        else
        {
            image.Save(outputStream, new PngEncoder());
        }

        return outputStream.ToArray();
    }

    public static bool IsValidImage(MemoryStream stream)
    {
        return IsValidImage(stream.ToArray());
    }

    private static bool IsValidImage(byte[] bytes)
    {
        return ValidImageBytes.Any(x => x.Value.SequenceEqual(bytes.Take(x.Value.Length)));
    }

    public static string? GetImageType(byte[] bytes)
    {
        var extension = ValidImageBytes.FirstOrDefault(x => x.Value.SequenceEqual(bytes.Take(x.Value.Length))).Key;
        return extension?[1..];
    }

    public static (bool, string?) IsHasValidImageAttributes(MemoryStream buffer)
    {
        if (buffer.Length > 5 * 1024 * 1024)
        {
            return (false, "UserFile is too large. Max size is 5MB");
        }

        if (!IsValidImage(buffer))
        {
            return (false, "Invalid image format");
        }

        return (true, null);
    }
}