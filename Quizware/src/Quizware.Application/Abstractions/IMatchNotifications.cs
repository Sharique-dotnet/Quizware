namespace Quizware.Application.Abstractions;

/// <summary>Queues a live notification (a /hubs/match event, 05-API-Design.md
/// §5.7) in the transactional outbox. The message is written in the caller's
/// unit of work, so it exists if and only if the change it announces was
/// saved; delivering it to SignalR is the outbox dispatcher's job (Phase 12).</summary>
public interface IMatchNotifications
{
    void Publish(string eventType, Guid programId, Guid matchId, object payload);
}
