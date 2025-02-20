using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Utils.Tools;

namespace Sunrise.API.Services;

public static class AssetService
{
    public static async Task<(bool, string?)> SetBanner(int userId, MemoryStream buffer)
    {
        var (isValid, err) = ImageTools.IsHasValidImageAttributes(buffer);
        if (!isValid || err != null)
            return (false, err);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var isSuccessful = await database.UserService.Files.SetBanner(userId, buffer.ToArray());
        return isSuccessful ? (true, null) : (false, "Failed to save banner. Please try again later.");
    }


    public static async Task<(bool, string?)> SetAvatar(int userId, MemoryStream buffer)
    {
        var (isValid, err) = ImageTools.IsHasValidImageAttributes(buffer);
        if (!isValid || err != null)
            return (false, err);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var isSuccessful = await database.UserService.Files.SetAvatar(userId, buffer.ToArray());

        return isSuccessful ? (true, null) : (false, "Failed to save avatar. Please try again later.");
    }
}