using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models.Scores;

namespace Sunrise.Shared.Database.Repositories;

public class ScoreSubmissionRequestRepository(SunriseDbContext dbContext)
{
    public async Task AddQueueEntry(ScoreSubmissionRequest payload, CancellationToken ct = default)
    {
        dbContext.ScoreSubmissionRequests.Add(payload);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<ScoreSubmissionRequest?> GetById(int payloadId, CancellationToken ct = default)
    {
        return await dbContext.ScoreSubmissionRequests.FindAsync([payloadId], ct);
    }

    public async Task DeleteById(int payloadId, CancellationToken ct = default)
    {
        await dbContext.ScoreSubmissionRequests
            .Where(e => e.Id == payloadId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<int?> GetUserIdByPayloadId(int payloadId, CancellationToken ct = default)
    {
        return await dbContext.ScoreSubmissionRequests
            .Where(p => p.Id == payloadId)
            .Select(p => (int?)p.UserId)
            .FirstOrDefaultAsync(ct);
    }
}