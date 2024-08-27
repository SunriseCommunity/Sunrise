using Sunrise.Server.Data;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public static class BaseApiService
{
    private const int MegaByte = 1024 * 1024;

    public static async Task SetAvatar(int userId, HttpRequest request)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted);

        ThrowIfInvalidImageFile(request, buffer);

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        await database.SetAvatar(userId, buffer.ToArray());
    }

    public static async Task SetBanner(int userId, HttpRequest request)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted);

        ThrowIfInvalidImageFile(request, buffer);

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        await database.SetBanner(userId, buffer.ToArray());
    }

    private static void ThrowIfInvalidImageFile(HttpRequest request, MemoryStream buffer)
    {
        if (buffer.Length > 5 * MegaByte)
        {
            throw new Exception("UserFile is too large. Max size is 5MB");
        }

        if (!ImageTools.IsValidImage(buffer))
        {
            throw new Exception("Invalid image format");
        }
    }
}