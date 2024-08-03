using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public class BaseApiService(ServicesProvider services)
{
    private const int MegaByte = 1024 * 1024;

    public async Task SetAvatar(int userId, HttpRequest request)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted);

        ThrowIfInvalidImageFile(request, buffer);

        await services.Database.SetAvatar(userId, buffer.ToArray());
    }

    public async Task SetBanner(int userId, HttpRequest request)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted);

        ThrowIfInvalidImageFile(request, buffer);

        await services.Database.SetBanner(userId, buffer.ToArray());
    }

    private void ThrowIfInvalidImageFile(HttpRequest request, MemoryStream buffer)
    {
        if (buffer.Length > 5 * MegaByte)
        {
            throw new Exception("UserFile is too large. Max size is 5MB");
        }

        var extension = request.Headers.ContentType.ToString().Split("/").Last();

        if (extension != "png" && extension != "jpg" && extension != "jpeg")
        {
            throw new Exception("Invalid file type. Only PNG and JPG are allowed");
        }
    }
}