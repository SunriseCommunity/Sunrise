using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Seeders;

namespace Sunrise.Shared.Database;

public static class DatabaseSeeder
{
    public static async Task UseAsyncSeeding(DbContext context, CancellationToken cancellationToken = default)
    {
        await MedalSeeder.SeedMedals(context, cancellationToken);
        await UserSeeder.SeedUsers(context, cancellationToken);
    }
}