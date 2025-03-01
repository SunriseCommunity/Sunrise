using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Repositories;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Repositories;

public class MedalRepository
{
    private readonly DatabaseService _databaseService;
    private readonly SunriseDbContext _dbContext;

    public MedalRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;
    }

    public async Task<List<Medal>> GetMedals(GameMode mode, QueryOptions? options = null)
    {
        return await _dbContext.Medals
            .Where(m => m.GameMode == mode || m.GameMode == null)
            .UseQueryOptions(options)
            .ToListAsync();
    }
    
    public async Task<Medal?> GetMedal(int medalId, QueryOptions? options = null)
    {
        return await _dbContext.Medals
            .Where(m => m.Id == medalId)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync();
    }

    public async Task<byte[]?> GetMedalImage(int medalFileId, bool isHighRes = false, QueryOptions? options = null)
    {
        var record = await _dbContext.MedalFiles
            .Where(r => r.Id == medalFileId)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync();

        if (record == null)
            return null;

        var imagePath = isHighRes ? record.Path.Replace(".png", "@2x.png") : record.Path;
        var file = await LocalStorageRepository.ReadFileAsync(Path.Combine(Configuration.DataPath, imagePath));

        return file;
    }
}