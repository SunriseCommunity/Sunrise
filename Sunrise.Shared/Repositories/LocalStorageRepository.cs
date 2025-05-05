using Microsoft.Extensions.Logging;

namespace Sunrise.Shared.Repositories;

public static class LocalStorageRepository
{
    private static readonly ILogger Logger;

    static LocalStorageRepository()
    {
        using var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        Logger = loggerFactory.CreateLogger("LocalStorageRepository");
    }

    public static async Task<bool> WriteFileAsync(string path, byte[] data, CancellationToken ct = default)
    {
        try
        {
            await File.WriteAllBytesAsync(path, data, ct);
            return true;
        }
        catch (Exception e)
        {
            Logger.LogError(e, $"Failed to write file to {path}");
            return false;
        }
    }
    
    public static async Task<bool> WriteFileAsync(string path, Stream dataStream, CancellationToken ct = default)
    {
        try
        {
            if (dataStream.CanSeek)
            {
                dataStream.Seek(0, SeekOrigin.Begin);
            }
            
            await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await dataStream.CopyToAsync(fileStream, ct);
            return true;
        }
        catch (Exception e)
        {
            Logger.LogError(e, $"Failed to write file to {path}");
            return false;
        }
    }

    public static async Task<byte[]?> ReadFileAsync(string path, CancellationToken ct = default)
    {
        try
        {
            return await File.ReadAllBytesAsync(path, ct);
        }
        catch (Exception e)
        {
            Logger.LogError(e, $"Failed to read file from {path}");
            return null;
        }
    }
}