using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
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
        await IncrementUserIds(context, ct);
        await context.SaveChangesAsync(ct);
    }

    private static async Task<List<(string, string)>> FetchUserForeignKeys(DbContext context)
    {
        var map = new List<(string, string)>();

        var query = @"
            SELECT TABLE_NAME, COLUMN_NAME
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE REFERENCED_COLUMN_NAME = 'Id' AND REFERENCED_TABLE_NAME = 'user';
        ";

        await using var connection = new MySqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync();

        await using var command = new MySqlCommand(query, connection);
        await using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var tableName = reader.GetString("TABLE_NAME");
            var columnName = reader.GetString("COLUMN_NAME");

            map.Add((tableName, columnName));
        }

        await connection.CloseAsync();

        return map;
    }

    private static async Task IncrementUserIds(DbContext context, CancellationToken ct = default)
    {
        var usersWithOldUserId = await context.Set<User>().Where(u => u.Id < 1000).AnyAsync(ct);

        if (!usersWithOldUserId) return;

        using var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        var logger = loggerFactory.CreateLogger<DbContext>();

        logger.LogWarning("Detected old user ID format (ID < 1000). Updating to the new format.");

        logger.LogCritical("NB!: Do NOT shut down the server during this process to avoid database corruption.");

        logger.LogInformation("Starting database backup as a precautionary measure.");

        await RecurringJobs.BackupDatabase(ct);

        logger.LogInformation("Database backup completed successfully.");

        await context.Database.ExecuteSqlRawAsync("ALTER TABLE `user` AUTO_INCREMENT = 1000;", ct);

        var foreignKeys = await FetchUserForeignKeys(context);

        await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=0;");

        var usersCount = await context.Set<User>().CountAsync(ct);

        var pageSize = 50;

        for (var x = 0;; x++)
        {
            var users = context.Set<User>()
                .OrderByDescending(u => u.Username)
                .Skip(x * pageSize)
                .Take(pageSize)
                .ToList();

            var caseStatements = new StringBuilder();
            var ids = new List<int>();

            foreach (var user in users)
            {
                var oldId = user.Id;
                var newId = oldId + 1000;
                ids.Add(oldId);

                caseStatements.Append($" WHEN {oldId} THEN {newId}");
            }

            var idsCsv = string.Join(", ", ids);

            foreach (var key in foreignKeys)
            {
                var sql = $@"
                            UPDATE {key.Item1}
                            SET {key.Item2} = CASE {key.Item2}
                                {caseStatements}
                                ELSE {key.Item2}
                            END
                            WHERE {key.Item2} IN ({idsCsv})
                            ";

                await context.Database.ExecuteSqlRawAsync(sql);
            }

            var userUpdateSql = $@"
                                UPDATE `user`
                                SET Id = CASE Id
                                    {caseStatements}
                                    ELSE Id
                                END
                                WHERE Id IN ({idsCsv})
                                ";

            await context.Database.ExecuteSqlRawAsync(userUpdateSql);

            logger.LogInformation($"Users updated: {x * pageSize + users.Count} / {usersCount}.");

            if (users.Count < pageSize) break;
        }

        await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=1;");

        logger.LogInformation("All user ids were updated to new ID format.");

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        await database.FlushAndUpdateRedisCache(false);
    }

    private static async Task SeedSunriseBot(DbContext context, CancellationToken ct = default)
    {
        var sunriseBot = new User
        {
            Username = Configuration.BotUsername,
            Description = "Let your smile be the sunshine that brightens the world around you.",
            Country = CountryCode.AQ,
            Privilege = UserPrivilege.ServerBot,
            Passhash = "12345678",
            Email = "bot@mail.com"
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
        var inputPath = Path.Combine(Configuration.DataPath, "Files/Assets/BotAvatar.png");
        var imagePath = $"Files/Avatars/{sunriseBot.Id}.png";
        var filePath = Path.Combine(Configuration.DataPath, imagePath);

        await using var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        if (!await LocalStorageRepository.WriteFileAsync(filePath, ImageTools.ResizeImage(inputStream, 256, 256), ct))
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