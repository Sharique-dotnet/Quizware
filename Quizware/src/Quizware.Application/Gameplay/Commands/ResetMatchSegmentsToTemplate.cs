using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Commands;

public sealed record ResetMatchSegmentsToTemplateCommand(Guid ProgramId, Guid MatchId) : IRequest<IReadOnlyList<MatchSegmentDto>>;

/// <summary>Throws away every per-match segment change and re-instantiates
/// from the stage's current templates, exactly as match creation does.</summary>
public sealed class ResetMatchSegmentsToTemplateCommandHandler
    : IRequestHandler<ResetMatchSegmentsToTemplateCommand, IReadOnlyList<MatchSegmentDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ResetMatchSegmentsToTemplateCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MatchSegmentDto>> Handle(
        ResetMatchSegmentsToTemplateCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, request.ProgramId, request.MatchId, cancellationToken);
        var actor = _currentUser.Email ?? "unknown";
        match.TouchSetup(actor);

        foreach (var segment in await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken))
        {
            segment.Delete(actor);
        }

        // The soft-deleted rows must be written first: the unique
        // (MatchId, OrderIndex) index only ignores rows already IsDeleted.
        await _db.SaveChangesAsync(cancellationToken);

        var stage = await _db.Stages.SingleAsync(s => s.Id == match.StageId, cancellationToken);
        var templates = await _db.StageSegmentTemplates.Where(t => t.StageId == stage.Id).ToListAsync(cancellationToken);
        var fresh = CreateMatchCommandHandler.InstantiateSegments(match, stage, templates, actor).ToList();
        _db.MatchSegments.AddRange(fresh);
        await _db.SaveChangesAsync(cancellationToken);

        return fresh.Select(MatchSetup.ToDto).ToList();
    }
}
