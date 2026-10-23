using MediatR;
using Quizware.Application.Qualification.Dtos;

namespace Quizware.Application.Qualification.Queries;

public sealed record GetQualificationPreviewQuery(Guid ProgramId, Guid StageId) : IRequest<QualificationPreviewDto>;

/// <summary>P11-01: who advances and why, before anything is committed.
/// Read-only — ties are shown with the criteria applied in memory; saving
/// them is <c>POST ties/detect</c>'s job.</summary>
public sealed class GetQualificationPreviewQueryHandler : IRequestHandler<GetQualificationPreviewQuery, QualificationPreviewDto>
{
    private readonly QualificationEvaluator _evaluator;

    public GetQualificationPreviewQueryHandler(QualificationEvaluator evaluator)
    {
        _evaluator = evaluator;
    }

    public async Task<QualificationPreviewDto> Handle(GetQualificationPreviewQuery request, CancellationToken cancellationToken) =>
        QualificationPreviewMapper.ToPreview(await _evaluator.EvaluateAsync(request.ProgramId, request.StageId, cancellationToken));
}
