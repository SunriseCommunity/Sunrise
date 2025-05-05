using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Utils.Tools;

namespace Sunrise.API.Services;

public class AssetService(DatabaseService database)
{
    public async Task<(bool, string?)> SetBanner(int userId, Stream fileStream)
    {
        var (isValid, err) = ImageTools.IsHasValidImageAttributes(fileStream);
        if (!isValid || err != null)
            return (false, err);
        
        var addOrUpdateBannerResult = await database.Users.Files.AddOrUpdateBanner(userId, fileStream);

        if (addOrUpdateBannerResult.IsFailure)
            return (false, "Failed to save banner. Please try again later.");

        return (true, null);
    }

    public async Task<(bool, string?)> SetAvatar(int userId, Stream fileStream)
    {
        var (isValid, err) = ImageTools.IsHasValidImageAttributes(fileStream);
        if (!isValid || err != null)
            return (false, err);

        var addOrUpdateAvatarResult = await database.Users.Files.AddOrUpdateAvatar(userId, fileStream);

        if (addOrUpdateAvatarResult.IsFailure)
            return (false, "Failed to save avatar. Please try again later.");

        return (true, null);
    }
}