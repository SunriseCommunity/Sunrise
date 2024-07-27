using Microsoft.AspNetCore.Http.HttpResults;

namespace Sunrise.WebClient.Services;

public class ApiService
{
    private const int MegaByte = 1024 * 1024;
    private readonly ServicesProvider _services;
    public ApiService(ServicesProvider services)
    {
        _services = services;
    }

    public async Task<byte[]> GetAvatarBytes(int id)
    {
        var avatar = await _services.Database.GetAvatar(id);

        if (avatar == null)
        {
            throw new Exception("Avatar not found");
        }

        return avatar;
    }


    public async Task<Exception?> SetAvatar(int userId, HttpRequest request)
    {
        using var buffer = new System.IO.MemoryStream();
        await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted);

        if (buffer.Length > 5 * MegaByte)
        {
            return new Exception("File is too large. Max size is 5MB");
        }

        var extension = request.Headers["Content-Type"].ToString().Split("/").Last();
        if (extension != "png" && extension != "jpg" && extension != "jpeg")
        {
            return new Exception("Invalid file type. Only PNG and JPG are allowed");
        }

        var avatar = buffer.ToArray();
        await _services.Database.SetAvatar(userId, avatar);
        return null;
    }


}