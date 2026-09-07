namespace QuizApp.Infrastructure.Idempotency;

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(Guid programId, string key, CancellationToken cancellationToken);

    Task SaveAsync(
        Guid programId, string key, string requestBodyHash, int responseStatusCode, string responseBody,
        CancellationToken cancellationToken);
}
