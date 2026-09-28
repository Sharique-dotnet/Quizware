using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay.Commands;

public sealed record CreateMatchCommand(Guid ProgramId, Guid StageId, string? Name, int MatchNumber) : IRequest<MatchDto>;

public sealed class CreateMatchCommandValidator : AbstractValidator<CreateMatchCommand>
{
    public CreateMatchCommandValidator()
    {
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(150);
        RuleFor(x => x.MatchNumber).GreaterThan(0);
    }
}

/// <summary>Segments are instantiated from the stage's templates here, not at
/// start, so the setup screens can reorder/add/remove them before play. The
/// match's RandomSeed is fixed now, so a RandomPerMatch order is resolved once
/// and never changes afterwards.</summary>
public sealed class CreateMatchCommandHandler : IRequestHandler<CreateMatchCommand, MatchDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateMatchCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MatchDto> Handle(CreateMatchCommand request, CancellationToken cancellationToken)
    {
        var stage = await _db.Stages
            .SingleOrDefaultAsync(s => s.Id == request.StageId && s.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage '{request.StageId}' was not found.");

        var numberTaken = await _db.Matches
            .AnyAsync(m => m.StageId == stage.Id && m.MatchNumber == request.MatchNumber, cancellationToken);
        if (numberTaken)
        {
            throw new InvalidStateTransitionException(
                $"Stage '{stage.Name}' already has a match numbered {request.MatchNumber}.");
        }

        var actor = _currentUser.Email ?? "unknown";
        var seed = Random.Shared.NextInt64(1, long.MaxValue);
        var name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        var match = Match.Create(request.ProgramId, stage.Id, request.MatchNumber, seed, actor, name: name);
        _db.Matches.Add(match);

        var templates = await _db.StageSegmentTemplates.Where(t => t.StageId == stage.Id).ToListAsync(cancellationToken);
        foreach (var segment in InstantiateSegments(match, stage, templates, actor))
        {
            _db.MatchSegments.Add(segment);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await MatchSetup.ToDetailAsync(_db, match, cancellationToken);
    }

    internal static IEnumerable<MatchSegment> InstantiateSegments(
        Match match, Stage stage, IReadOnlyList<StageSegmentTemplate> templates, string actor)
    {
        var byId = templates.ToDictionary(t => t.Id);
        var order = SegmentOrderResolver.Resolve(
            templates.Select(t => new SegmentOrderInput(t.Id, t.OrderIndex, t.IsOrderLocked)).ToList(),
            stage.SegmentOrderMode,
            match.RandomSeed);

        for (var i = 0; i < order.Count; i++)
        {
            var template = byId[order[i]];
            var segment = MatchSegment.Create(
                match.ProgramId, match.Id, template.Id, template.FormatCode, i, template.QuestionCount, actor);
            if (template.IsOrderLocked)
            {
                segment.Lock();
            }

            yield return segment;
        }
    }
}
