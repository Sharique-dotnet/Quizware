using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Qualification.Dtos;
using Quizware.Domain.Common.Exceptions;

namespace Quizware.Application.Qualification.Commands;

public sealed record DetectTiesCommand(Guid ProgramId, Guid StageId) : IRequest<StageTiesDto>;

/// <summary>P11-02/03: re-runs tie detection on the completed stage and saves
/// the result — the criteria phase included, so a tie the criteria settle is
/// resolved with its deciding criterion recorded. Running it again changes
/// nothing unless the standings did; a saved tie that no longer matches is
/// abandoned.</summary>
public sealed class DetectTiesCommandHandler : IRequestHandler<DetectTiesCommand, StageTiesDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly QualificationEvaluator _evaluator;
    private readonly TieViews _views;

    public DetectTiesCommandHandler(IAppDbContext db, ICurrentUser currentUser, QualificationEvaluator evaluator, TieViews views)
    {
        _db = db;
        _currentUser = currentUser;
        _evaluator = evaluator;
        _views = views;
    }

    public async Task<StageTiesDto> Handle(DetectTiesCommand request, CancellationToken cancellationToken)
    {
        await DetectAsync(_db, _currentUser, _evaluator, request.ProgramId, request.StageId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await _views.ForStageAsync(request.ProgramId, request.StageId, cancellationToken);
    }

    /// <summary>Shared with the commit, which detects in the same transaction
    /// before it decides. Does not save.</summary>
    public static async Task<QualificationEvaluation> DetectAsync(
        IAppDbContext db, ICurrentUser currentUser, QualificationEvaluator evaluator, Guid programId, Guid stageId,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("An authenticated user is required to detect ties.");
        var actor = currentUser.Email ?? "unknown";
        var preview = await evaluator.EvaluateAsync(programId, stageId, cancellationToken);
        if (!preview.AllMatchesComplete)
        {
            throw new InvalidStateTransitionException("Ties can only be detected once every match in the stage is completed.");
        }

        var recorder = await TieRecorder.CreateAsync(db, evaluator, programId, stageId, userId, actor, cancellationToken);
        return await evaluator.EvaluateAsync(programId, stageId, recorder, cancellationToken);
    }
}
