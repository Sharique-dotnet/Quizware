using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Tournament.Dtos;
using QuizApp.Domain.Enums;

namespace QuizApp.Application.Tournament.Commands;

/// <summary>P7-03: takes the full ordered list of a stage's segment template
/// ids. A partial list is rejected. Locked segments keep their original
/// OrderIndex regardless of where they appear in the request — unlocked
/// segments are packed into the remaining slots, in the order given. This
/// only ever changes the *template*; matches already created keep the
/// MatchSegment order they were instantiated with (Phase 9), so the
/// response reports counts rather than mutating any Match.</summary>
public sealed record ReorderSegmentTemplatesCommand(
    Guid ProgramId, Guid StageId, IReadOnlyList<Guid> OrderedSegmentTemplateIds) : IRequest<ReorderSegmentTemplatesResultDto>;

public sealed class ReorderSegmentTemplatesCommandValidator : AbstractValidator<ReorderSegmentTemplatesCommand>
{
    public ReorderSegmentTemplatesCommandValidator()
    {
        RuleFor(x => x.OrderedSegmentTemplateIds).NotEmpty();
    }
}

public sealed class ReorderSegmentTemplatesCommandHandler
    : IRequestHandler<ReorderSegmentTemplatesCommand, ReorderSegmentTemplatesResultDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ReorderSegmentTemplatesCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ReorderSegmentTemplatesResultDto> Handle(
        ReorderSegmentTemplatesCommand request, CancellationToken cancellationToken)
    {
        var stage = await _db.Stages
            .SingleOrDefaultAsync(s => s.Id == request.StageId && s.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage '{request.StageId}' was not found.");

        var segments = await _db.StageSegmentTemplates.Where(s => s.StageId == stage.Id).ToListAsync(cancellationToken);

        var requested = request.OrderedSegmentTemplateIds;
        if (segments.Count != requested.Count
            || requested.Distinct().Count() != requested.Count
            || requested.Any(id => segments.All(s => s.Id != id)))
        {
            throw new Application.Common.Exceptions.ValidationException(
                new Dictionary<string, string[]>
                {
                    ["orderedSegmentTemplateIds"] = ["Must list every segment of this stage exactly once."],
                });
        }

        var byId = segments.ToDictionary(s => s.Id);
        var lockedOriginalSlots = segments
            .Where(s => s.IsOrderLocked)
            .Select(s => s.OrderIndex)
            .ToHashSet();
        var unlockedQueue = new Queue<Guid>(requested.Where(id => !byId[id].IsOrderLocked));

        // Same UX_StageSegmentTemplate_Stage_Order collision issue as
        // ReorderStagesCommandHandler — offset first, then apply final slots.
        var actor = _currentUser.Email ?? "unknown";
        var offsetQueue = new Queue<Guid>(unlockedQueue);
        for (var slot = 0; slot < segments.Count; slot++)
        {
            if (!lockedOriginalSlots.Contains(slot))
            {
                byId[offsetQueue.Dequeue()].Reorder(100_000 + slot, actor);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        for (var slot = 0; slot < segments.Count; slot++)
        {
            if (lockedOriginalSlots.Contains(slot))
            {
                continue; // the locked segment already occupies this slot; leave its OrderIndex untouched.
            }

            var nextId = unlockedQueue.Dequeue();
            byId[nextId].Reorder(slot, actor);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var alreadyCreated = await _db.Matches.CountAsync(m => m.StageId == stage.Id, cancellationToken);
        var inProgress = await _db.Matches.CountAsync(
            m => m.StageId == stage.Id && m.State == MatchState.InProgress, cancellationToken);

        return new ReorderSegmentTemplatesResultDto(
            stage.Id,
            stage.SegmentOrderMode.ToString(),
            segments.OrderBy(s => s.OrderIndex).Select(s => s.ToDto()).ToList(),
            "Matches not yet created will pick up the new segment order automatically.",
            alreadyCreated,
            inProgress);
    }
}
