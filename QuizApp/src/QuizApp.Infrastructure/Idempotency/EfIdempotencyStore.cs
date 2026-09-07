using Microsoft.EntityFrameworkCore;
using QuizApp.Infrastructure.Persistence;

namespace QuizApp.Infrastructure.Idempotency;

public sealed class EfIdempotencyStore : IIdempotencyStore
{
    private readonly AppDbContext _dbContext;

    public EfIdempotencyStore(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IdempotencyRecord?> FindAsync(Guid programId, string key, CancellationToken cancellationToken) =>
        _dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.ProgramId == programId && r.Key == key, cancellationToken);

    public async Task SaveAsync(
        Guid programId, string key, string requestBodyHash, int responseStatusCode, string responseBody,
        CancellationToken cancellationToken)
    {
        _dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            Key = key,
            RequestBodyHash = requestBodyHash,
            ResponseStatusCode = responseStatusCode,
            ResponseBody = responseBody,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
