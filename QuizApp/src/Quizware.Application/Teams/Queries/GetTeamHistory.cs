using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Teams.Dtos;

namespace Quizware.Application.Teams.Queries;

/// <summary>Real query against real tables (MatchParticipant, Match,
/// TeamMatchScore all exist since Phase 4) — it will correctly return an
/// empty list until Phase 9/10's match engine and scoring exist to
/// populate them. Not a stub, just honestly empty for now.</summary>
public sealed record GetTeamHistoryQuery(Guid ProgramId, Guid TeamId) : IRequest<IReadOnlyList<TeamHistoryEntryDto>>;

public sealed class GetTeamHistoryQueryHandler : IRequestHandler<GetTeamHistoryQuery, IReadOnlyList<TeamHistoryEntryDto>>
{
    private readonly IAppDbContext _db;

    public GetTeamHistoryQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TeamHistoryEntryDto>> Handle(
        GetTeamHistoryQuery request, CancellationToken cancellationToken)
    {
        if (!await _db.Teams.AnyAsync(t => t.Id == request.TeamId && t.ProgramId == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Team '{request.TeamId}' was not found.");
        }

        var entries = await (
            from mp in _db.MatchParticipants
            where mp.TeamId == request.TeamId
            join match in _db.Matches on mp.MatchId equals match.Id
            where match.CompletedAtUtc != null
            join score in _db.TeamMatchScores on new { match.Id, TeamId = request.TeamId }
                equals new { Id = score.MatchId, score.TeamId } into scores
            from score in scores.DefaultIfEmpty()
            orderby match.CompletedAtUtc
            select new TeamHistoryEntryDto(
                match.Id,
                match.Name ?? $"Match {match.MatchNumber}",
                score != null ? score.TotalPoints : 0,
                match.CompletedAtUtc!.Value))
            .ToListAsync(cancellationToken);

        return entries;
    }
}
