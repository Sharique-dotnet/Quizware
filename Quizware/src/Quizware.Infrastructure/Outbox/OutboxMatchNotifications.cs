using System.Text.Json;
using Quizware.Application.Abstractions;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Infrastructure.Outbox;

/// <summary>Adds an OutboxMessage to the request's AppDbContext — the same
/// instance the Application handlers save through — so it commits in the same
/// transaction as the change it announces.</summary>
public sealed class OutboxMatchNotifications : IMatchNotifications
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;

    public OutboxMatchNotifications(AppDbContext db)
    {
        _db = db;
    }

    public void Publish(string eventType, Guid programId, Guid matchId, object payload)
    {
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = eventType,
            PayloadJson = JsonSerializer.Serialize(new { programId, matchId, data = payload }, JsonOptions),
            OccurredAtUtc = DateTime.UtcNow,
        });
    }
}
