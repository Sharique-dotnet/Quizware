using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Tournament.Dtos;
using Quizware.Domain.Common.Exceptions;

namespace Quizware.Application.Tournament.Commands;

/// <summary>UX_Stage_Program_Order is maintained by reassigning every
/// stage's OrderIndex inside one SaveChanges call — a partial list (missing
/// any of the program's stages) is rejected outright, matching P7-01's
/// acceptance criterion.</summary>
public sealed record ReorderStagesCommand(Guid ProgramId, IReadOnlyList<Guid> OrderedStageIds) : IRequest<IReadOnlyList<StageSummaryDto>>;

public sealed class ReorderStagesCommandValidator : AbstractValidator<ReorderStagesCommand>
{
    public ReorderStagesCommandValidator()
    {
        RuleFor(x => x.OrderedStageIds).NotEmpty();
    }
}

public sealed class ReorderStagesCommandHandler : IRequestHandler<ReorderStagesCommand, IReadOnlyList<StageSummaryDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ReorderStagesCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<StageSummaryDto>> Handle(ReorderStagesCommand request, CancellationToken cancellationToken)
    {
        var stages = await _db.Stages.Where(s => s.ProgramId == request.ProgramId).ToListAsync(cancellationToken);

        if (stages.Count != request.OrderedStageIds.Count || request.OrderedStageIds.Distinct().Count() != request.OrderedStageIds.Count)
        {
            throw new Application.Common.Exceptions.ValidationException(
                new Dictionary<string, string[]>
                {
                    ["orderedStageIds"] = ["Must list every stage in the program exactly once."],
                });
        }

        var stagesById = stages.ToDictionary(s => s.Id);
        if (request.OrderedStageIds.Any(id => !stagesById.ContainsKey(id)))
        {
            throw new InvalidStateTransitionException("One or more stage ids do not belong to this program.");
        }

        // UX_Stage_Program_Order is a unique (ProgramId, OrderIndex) index,
        // checked per-statement (not deferred) by both SQL Server and the
        // SQLite test provider — writing final indices directly can collide
        // mid-batch with another stage's current index. A temporary offset
        // well outside any real OrderIndex avoids that, then a second save
        // applies the real values.
        var actor = _currentUser.Email ?? "unknown";
        for (var i = 0; i < request.OrderedStageIds.Count; i++)
        {
            stagesById[request.OrderedStageIds[i]].Reorder(100_000 + i, actor);
        }

        await _db.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < request.OrderedStageIds.Count; i++)
        {
            stagesById[request.OrderedStageIds[i]].Reorder(i, actor);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return stages.OrderBy(s => s.OrderIndex).Select(s => s.ToSummaryDto()).ToList();
    }
}
