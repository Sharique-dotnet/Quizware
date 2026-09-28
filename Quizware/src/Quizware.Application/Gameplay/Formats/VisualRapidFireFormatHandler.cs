using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>A run of images. The contract has one media URL, so each image is
/// listed as an option whose Text is that image's URL. Answers are judged per
/// image by the operator.</summary>
public sealed class VisualRapidFireFormatHandler : IQuestionFormatHandler
{
    private readonly IAppDbContext _db;

    public VisualRapidFireFormatHandler(IAppDbContext db)
    {
        _db = db;
    }

    public QuestionFormatCode Format => QuestionFormatCode.VisualRapidFire;

    public bool AnyTeamMayAnswer => false;

    public int? DefaultTimeLimitSeconds(Question question) => null;

    public bool WrongAnswerLeavesQuestionOpen(Question question) => false;

    public async Task<FormatContent> PresentAsync(Question question, MatchQuestion matchQuestion, CancellationToken cancellationToken)
    {
        var items = await _db.VisualRapidFireItems.Where(i => i.QuestionId == question.Id).OrderBy(i => i.DisplayOrder).ToListAsync(cancellationToken);
        var options = items.Select((item, i) => new CurrentQuestionOptionDto(item.Id, QuestionFormatHandlers.MediaUrl(item.MediaAssetId), i)).ToList();
        return new FormatContent(options, null, null);
    }

    public Task<ResponseEvaluation> EvaluateAsync(
        Question question, IReadOnlyList<Guid>? selectedIds, string? freeText, CancellationToken cancellationToken) =>
        Task.FromResult(ResponseEvaluation.NotChecked);
}
