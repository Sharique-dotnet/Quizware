using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Rules.Dtos;

namespace Quizware.Application.Rules.Queries;

public sealed record GetScoringRulesQuery(Guid ProgramId) : IRequest<IReadOnlyList<ScoringRuleAppDto>>;

public sealed class GetScoringRulesQueryHandler : IRequestHandler<GetScoringRulesQuery, IReadOnlyList<ScoringRuleAppDto>>
{
    private readonly IAppDbContext _db;

    public GetScoringRulesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ScoringRuleAppDto>> Handle(GetScoringRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await _db.ScoringRules
            .Where(r => r.ProgramId == request.ProgramId && r.StageId == null && r.SegmentTemplateId == null)
            .ToListAsync(cancellationToken);
        return rules.Select(r => r.ToDto()).ToList();
    }
}

public sealed record GetSelectionRulesQuery(Guid ProgramId) : IRequest<IReadOnlyList<SelectionRuleAppDto>>;

public sealed class GetSelectionRulesQueryHandler : IRequestHandler<GetSelectionRulesQuery, IReadOnlyList<SelectionRuleAppDto>>
{
    private readonly IAppDbContext _db;

    public GetSelectionRulesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SelectionRuleAppDto>> Handle(GetSelectionRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await _db.QuestionSelectionRules
            .Where(r => r.ProgramId == request.ProgramId)
            .ToListAsync(cancellationToken);
        return rules.Select(r => r.ToDto()).ToList();
    }
}

public sealed record GetQualificationRulesQuery(Guid ProgramId) : IRequest<IReadOnlyList<QualificationRuleAppDto>>;

public sealed class GetQualificationRulesQueryHandler : IRequestHandler<GetQualificationRulesQuery, IReadOnlyList<QualificationRuleAppDto>>
{
    private readonly IAppDbContext _db;

    public GetQualificationRulesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<QualificationRuleAppDto>> Handle(GetQualificationRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await _db.QualificationRules
            .Where(r => r.ProgramId == request.ProgramId)
            .ToListAsync(cancellationToken);
        return rules.Select(r => r.ToDto()).ToList();
    }
}

public sealed record GetTieBreakRulesQuery(Guid ProgramId) : IRequest<IReadOnlyList<TieBreakRuleAppDto>>;

public sealed class GetTieBreakRulesQueryHandler : IRequestHandler<GetTieBreakRulesQuery, IReadOnlyList<TieBreakRuleAppDto>>
{
    private readonly IAppDbContext _db;

    public GetTieBreakRulesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TieBreakRuleAppDto>> Handle(GetTieBreakRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await _db.TieBreakRules
            .Where(r => r.ProgramId == request.ProgramId)
            .ToListAsync(cancellationToken);
        return rules.Select(r => r.ToDto()).ToList();
    }
}
