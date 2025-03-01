using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils.Tools;

namespace Sunrise.Shared.Database.Seeders;

public static class UserSeeder
{
    public static async Task SeedUsers(DbContext context, CancellationToken ct = default)
    {
        await SeedSunriseBot(context, ct);
        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedSunriseBot(DbContext context, CancellationToken ct = default)
    {
        var sunriseBot = new User
        {
            Username = Configuration.BotUsername,
            Description = "Let your smile be the sunshine that brightens the world around you.",
            Country = (short)CountryCode.AQ,
            Privilege = UserPrivilege.ServerBot,
            Passhash = "12345678",
            Email = "bot@mail.com",
            AccountStatus = UserAccountStatus.Restricted // TODO: Find a better way to handle this
        };

        var sunriseBotEntry = await context.Set<User>().Where(x => x.Privilege.HasFlag(UserPrivilege.ServerBot)).FirstOrDefaultAsync(ct);

        if (sunriseBotEntry != null)
        {
            sunriseBot.Id = sunriseBotEntry.Id;
            sunriseBot.RegisterDate = sunriseBotEntry.RegisterDate;
            context.Entry(sunriseBotEntry).CurrentValues.SetValues(sunriseBot);

            context.Set<User>().Update(sunriseBotEntry);
        }
        else
        {
            var usersWithBotCredentials =
                await context.Set<User>().Where(x => x.Username == sunriseBot.Username || x.Email == sunriseBot.Email).ToListAsync(cancellationToken: ct);

            if (usersWithBotCredentials.Any())
            {
                foreach (var user in usersWithBotCredentials)
                {
                    Console.WriteLine($"User {user.Username} (id: {user.Id}) has same credential as Sunrise Bot, while not having {nameof(UserPrivilege.ServerBot)} privileges.");
                }

                throw new Exception("Error while creating sunrise bot in database. Remove or edit users above to continue.");
            }

            await context.Set<User>().AddAsync(sunriseBot, ct);
            await context.SaveChangesAsync(ct);
            await AddSunriseBotAvatar(context, sunriseBot, ct);
        }

        await context.SaveChangesAsync(ct);
    }

    private static async Task AddSunriseBotAvatar(DbContext context, User sunriseBot, CancellationToken ct = default)
    {
        var sunriseBotAvatar = await File.ReadAllBytesAsync(Path.Combine(Configuration.DataPath, "Files/Assets/BotAvatar.png"), ct);
        var imagePath = $"Files/Avatars/{sunriseBot.Id}.png";
        var filePath = Path.Combine(Configuration.DataPath, imagePath);

        if (!await LocalStorageRepository.WriteFileAsync(filePath, ImageTools.ResizeImage(sunriseBotAvatar, 256, 256), ct))
            throw new ApplicationException(QueryResultError.CREATING_FILE_FAILED);

        var record = new UserFile
        {
            OwnerId = sunriseBot.Id,
            Path = imagePath,
            Type = FileType.Avatar
        };

        await context.Set<UserFile>().AddAsync(record, ct);
        await context.SaveChangesAsync(ct);
    }
}