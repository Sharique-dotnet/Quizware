using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Rules.Dtos;
using QuizApp.Domain.Scoring;

namespace QuizApp.Application.Rules.Commands;

public sealed record ResetScoringDefaultsCommand(Guid ProgramId) : IRequest<IReadOnlyList<ScoringRuleAppDto>>;

public sealed class ResetScoringDefaultsCommandHandler : IRequestHandler<ResetScoringDefaultsCommand, IReadOnlyList<ScoringRuleAppDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ResetScoringDefaultsCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ScoringRuleAppDto>> Handle(ResetScoringDefaultsCommand request, CancellationToken cancellationToken)
    {
        var actor = _currentUser.Email ?? "unknown";
        var existing = await _db.ScoringRules
            .Where(r => r.ProgramId == request.ProgramId && r.StageId == null && r.SegmentTemplateId == null)
            .ToListAsync(cancellationToken);
        foreach (var rule in existing)
        {
            rule.Delete(actor);
        }

        foreach (var value in QuizApp.Domain.Scoring.DefaultScoringValues.All)
        {
            _db.ScoringRules.Add(ScoringRule.Create(request.ProgramId, value.FormatCode, value.Outcome, value.Points, actor, contextKey: value.ContextKey));
        }

        await _db.SaveChangesAsync(cancellationToken);

        var all = await _db.ScoringRules
            .Where(r => r.ProgramId == request.ProgramId && r.StageId == null && r.SegmentTemplateId == null)
            .ToListAsync(cancellationToken);
        return all.Select(r => r.ToDto()).ToList();
    }
}
