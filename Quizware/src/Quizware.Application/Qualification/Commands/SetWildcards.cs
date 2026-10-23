using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Qualification.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Qualification;
using ValidationException = Quizware.Application.Common.Exceptions.ValidationException;

namespace Quizware.Application.Qualification.Commands;

public sealed record SetWildcardsCommand(Guid ProgramId, Guid StageId, IReadOnlyList<Guid> TeamIds) : IRequest<QualificationPreviewDto>;

public sealed class SetWildcardsCommandValidator : AbstractValidator<SetWildcardsCommand>
{
    public SetWildcardsCommandValidator()
    {
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.TeamIds).NotNull();
        RuleFor(x => x.TeamIds).Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("A team can only be picked once.");
    }
}

/// <summary>Replaces the stage's manual wildcard picks. Each pick must be a
/// team that took part in the stage and did not otherwise qualify, and there
/// can be no more picks than the rule's <c>ManualWildcardSlots</c>. Picks are
/// stored as uncommitted qualification rows until the commit.</summary>
public sealed class SetWildcardsCommandHandler : IRequestHandler<SetWildcardsCommand, QualificationPreviewDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly QualificationEvaluator _evaluator;

    public SetWildcardsCommandHandler(IAppDbContext db, ICurrentUser currentUser, QualificationEvaluator evaluator)
    {
        _db = db;
        _currentUser = currentUser;
        _evaluator = evaluator;
    }

    public async Task<QualificationPreviewDto> Handle(SetWildcardsCommand request, CancellationToken cancellationToken)
    {
        var actor = _currentUser.Email ?? "unknown";
        var evaluation = await _evaluator.EvaluateAsync(request.ProgramId, request.StageId, cancellationToken);
        if (evaluation.AlreadyCommitted)
        {
            throw new InvalidStateTransitionException("Qualification from this stage is already committed; roll it back before changing wildcards.");
        }

        if (request.TeamIds.Count > evaluation.Rule.ManualWildcardSlots)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["teamIds"] = [$"The rule allows {evaluation.Rule.ManualWildcardSlots} manual wildcard(s); {request.TeamIds.Count} were picked."],
            });
        }

        var eligible = evaluation.Eliminated
            .Concat(evaluation.Qualifiers.Where(q => q.Reason == QualificationReason.Manual).Select(q => q.TeamId))
            .ToHashSet();
        var ineligible = request.TeamIds.Where(id => !eligible.Contains(id)).ToList();
        if (ineligible.Count > 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["teamIds"] = [$"Only teams that took part in the stage and did not otherwise qualify can be wildcards: {string.Join(", ", ineligible)}."],
            });
        }

        var existing = await _db.StageQualifications
            .Where(q => q.FromStageId == request.StageId && q.QualificationReason == QualificationReason.Manual && !q.IsCommitted)
            .ToListAsync(cancellationToken);
        foreach (var pick in existing)
        {
            pick.Delete(actor);
        }

        foreach (var teamId in request.TeamIds)
        {
            var standing = evaluation.Standing[teamId];
            _db.StageQualifications.Add(StageQualification.Create(
                request.ProgramId, request.StageId, teamId, QualificationReason.Manual, standing.Score, standing.Rank, actor,
                evaluation.ToStage?.Id, evaluation.MatchOfTeam[teamId].Id));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return QualificationPreviewMapper.ToPreview(await _evaluator.EvaluateAsync(request.ProgramId, request.StageId, cancellationToken));
    }
}
