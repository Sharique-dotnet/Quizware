using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Common.Exceptions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Enums;

namespace Quizware.Application.Gameplay.Queries;

public sealed record ListMatchesQuery(Guid ProgramId, Guid? StageId, string? State) : IRequest<IReadOnlyList<MatchSummaryDto>>;

public sealed class ListMatchesQueryHandler : IRequestHandler<ListMatchesQuery, IReadOnlyList<MatchSummaryDto>>
{
    private readonly IAppDbContext _db;

    public ListMatchesQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MatchSummaryDto>> Handle(ListMatchesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Matches.Where(m => m.ProgramId == request.ProgramId);

        if (request.StageId is not null)
        {
            query = query.Where(m => m.StageId == request.StageId);
        }

        if (!string.IsNullOrWhiteSpace(request.State))
        {
            if (!Enum.TryParse<MatchState>(request.State, ignoreCase: true, out var state))
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["state"] = [$"State must be one of: {string.Join(", ", Enum.GetNames<MatchState>())}."],
                });
            }

            query = query.Where(m => m.State == state);
        }

        var matches = await query.OrderBy(m => m.StageId).ThenBy(m => m.MatchNumber).ToListAsync(cancellationToken);
        return matches
            .Select(m => new MatchSummaryDto(m.Id, MatchSetup.DisplayName(m), m.StageId, m.State.ToString(), m.MatchNumber))
            .ToList();
    }
}
