using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Repositories;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Repositories;

public class MedalRepository(SunriseDbContext dbContext)
{
    public async Task<List<Medal>> GetMedals(GameMode mode, QueryOptions? options = null, CancellationToken ct = default)
    {
        return await dbContext.Medals
            .Where(m => m.GameMode == mode || m.GameMode == null)
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<Medal?> GetMedal(int medalId, QueryOptions? options = null, CancellationToken ct = default)
    {
        return await dbContext.Medals
            .Where(m => m.Id == medalId)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<byte[]?> GetMedalImage(int medalFileId, bool isHighRes = false, QueryOptions? options = null, CancellationToken ct = default)
    {
        var record = await dbContext.MedalFiles
            .Where(r => r.Id == medalFileId)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync(cancellationToken: ct);

        if (record == null)
            return null;

        var imagePath = isHighRes ? record.Path.Replace(".png", "@2x.png") : record.Path;
        var file = await LocalStorageRepository.ReadFileAsync(Path.Combine(Configuration.DataPath, imagePath), ct);

        return file;
    }
}