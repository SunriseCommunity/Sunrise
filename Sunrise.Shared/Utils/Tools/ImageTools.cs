using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace Sunrise.Shared.Utils.Tools;

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

    public static byte[] ResizeImage(Stream fileStream, int width, int height)
    {
        if (!fileStream.CanSeek)
            throw new InvalidOperationException("Input stream must be seekable.");
        
        fileStream.Position = 0;

        var imageFormat = Image.DetectFormat(fileStream);
        fileStream.Position = 0;

        using var image = Image.Load(fileStream);

        var options = new ResizeOptions
        {
            Size = new Size(width, height),
            Mode = ResizeMode.Crop
        };

        image.Mutate(x => x.Resize(options));

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

    public static bool IsValidImage(Stream fileStream)
    {
        if (!fileStream.CanSeek)
            throw new InvalidOperationException("Input stream must be seekable.");
        
        fileStream.Position = 0;
        
        var maxHeaderSize = ValidImageBytes.Max(x => x.Value.Length);
        var header = new byte[maxHeaderSize];

        var bytesRead = fileStream.Read(header, 0, maxHeaderSize);

        fileStream.Position = 0;

        return ValidImageBytes.Any(x => 
            bytesRead >= x.Value.Length && 
            x.Value.SequenceEqual(header.Take(x.Value.Length)));
    }

    public static string? GetImageType(byte[] bytes)
    {
        var extension = ValidImageBytes.FirstOrDefault(x => x.Value.SequenceEqual(bytes.Take(x.Value.Length))).Key;
        return extension?[1..];
    }
    
    public static string? GetImageType(Stream fileStream)
    {
        if (!fileStream.CanSeek)
            throw new InvalidOperationException("Input stream must be seekable.");
        
        fileStream.Position = 0;

        var maxHeaderLength = ValidImageBytes.Max(x => x.Value.Length);
        var buffer = new byte[maxHeaderLength];
        var bytesRead = fileStream.Read(buffer, 0, maxHeaderLength);

        fileStream.Position = 0;

        var match = ValidImageBytes.FirstOrDefault(x =>
            bytesRead >= x.Value.Length &&
            x.Value.SequenceEqual(buffer.Take(x.Value.Length)));

        return match.Key?[1..];
    }

    public static (bool, string?) IsHasValidImageAttributes(Stream stream)
    {
        if (stream.Length > 5 * 1024 * 1024)
        {
            return (false, "UserFile is too large. Max size is 5MB");
        }

        if (!IsValidImage(stream))
        {
            return (false, "Invalid image format");
        }

        return (true, null);
    }
}