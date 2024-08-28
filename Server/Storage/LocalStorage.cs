namespace Sunrise.Server.Storage;

public static class LocalStorage
{
    private static readonly ILogger Logger;

    static LocalStorage()
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        Logger = loggerFactory.CreateLogger("LocalStorage");
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