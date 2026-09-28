using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Commands;

public sealed record RevealQuestionCommand(Guid MatchId, Guid MatchQuestionId) : IRequest<CurrentQuestionDto>;

/// <summary>After this, display screens receive the correct answer too.</summary>
public sealed class RevealQuestionCommandHandler : IRequestHandler<RevealQuestionCommand, CurrentQuestionDto>
{
    private readonly IAppDbContext _db;
    private readonly MatchEventLog _eventLog;
    private readonly LiveStateBuilder _state;

    public RevealQuestionCommandHandler(IAppDbContext db, MatchEventLog eventLog, LiveStateBuilder state)
    {
        _db = db;
        _eventLog = eventLog;
        _state = state;
    }

    public async Task<CurrentQuestionDto> Handle(RevealQuestionCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        LiveRules.RequireInProgress(match);
        var question = await LiveRules.LoadQuestionAsync(_db, match.Id, request.MatchQuestionId, cancellationToken);

        question.Reveal();
        await _eventLog.AppendAsync(match, MatchEventTypes.QuestionRevealed, new { matchQuestionId = question.Id }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await _state.PresentAsync(question, cancellationToken);
    }
}
