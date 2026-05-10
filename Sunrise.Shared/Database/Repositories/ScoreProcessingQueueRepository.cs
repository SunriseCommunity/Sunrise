using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models.Scores;

namespace Sunrise.Shared.Database.Repositories;

public class ScoreProcessingQueueRepository(SunriseDbContext dbContext)
{
    public async Task AddQueueEntry(ScoreProcessingQueue payload, CancellationToken ct = default)
    {
        dbContext.ScoreProcessingQueue.Add(payload);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<ScoreProcessingQueue?> GetById(int payloadId, CancellationToken ct = default)
    {
        return await dbContext.ScoreProcessingQueue.FindAsync([payloadId], ct);
    }

    public async Task DeleteById(int payloadId, CancellationToken ct = default)
    {
        await dbContext.ScoreProcessingQueue
            .Where(e => e.Id == payloadId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<int?> GetUserIdByPayloadId(int payloadId, CancellationToken ct = default)
    {
        return await dbContext.ScoreProcessingQueue
            .Where(p => p.Id == payloadId)
            .Select(p => (int?)p.UserId)
            .FirstOrDefaultAsync(ct);
    }
}