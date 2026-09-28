using FluentValidation;
using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Commands;

public sealed record SkipQuestionCommand(Guid MatchId, Guid MatchQuestionId, string Reason) : IRequest<LiveMatchStateDto>;

public sealed class SkipQuestionCommandValidator : AbstractValidator<SkipQuestionCommand>
{
    public SkipQuestionCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Takes the question off screen with no answer and no points. The
/// turn still counts, so the rotation moves on to the next team.</summary>
public sealed class SkipQuestionCommandHandler : IRequestHandler<SkipQuestionCommand, LiveMatchStateDto>
{
    private readonly IAppDbContext _db;
    private readonly MatchEventLog _eventLog;
    private readonly LiveStateBuilder _state;

    public SkipQuestionCommandHandler(IAppDbContext db, MatchEventLog eventLog, LiveStateBuilder state)
    {
        _db = db;
        _eventLog = eventLog;
        _state = state;
    }

    public async Task<LiveMatchStateDto> Handle(SkipQuestionCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        LiveRules.RequireInProgress(match);
        var question = await LiveRules.LoadQuestionAsync(_db, match.Id, request.MatchQuestionId, cancellationToken);

        question.Skip();
        await _eventLog.AppendAsync(match, MatchEventTypes.QuestionSkipped, new { matchQuestionId = question.Id, reason = request.Reason }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await _state.BuildAsync(match, cancellationToken);
    }
}
