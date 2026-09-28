using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.QuestionBank;
using ValidationException = Quizware.Application.Common.Exceptions.ValidationException;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>The six formats whose answer is one or more of their own options
/// (QuestionOption rows): options shown in the per-match shuffle fixed at
/// reservation, the single correct option named when there is exactly one,
/// and a selection checked against the full set of correct options.</summary>
public abstract class OptionFormatHandler : IQuestionFormatHandler
{
    private readonly IAppDbContext _db;

    protected OptionFormatHandler(IAppDbContext db)
    {
        _db = db;
    }

    public abstract QuestionFormatCode Format { get; }

    public virtual bool AnyTeamMayAnswer => false;

    public virtual int? DefaultTimeLimitSeconds(Question question) => null;

    public virtual bool WrongAnswerLeavesQuestionOpen(Question question) => false;

    /// <summary>Whether options appear in the per-match shuffled order.</summary>
    protected virtual bool ShuffleOptions(Question question) => true;

    public async Task<FormatContent> PresentAsync(Question question, MatchQuestion matchQuestion, CancellationToken cancellationToken)
    {
        var options = await _db.QuestionOptions.Where(o => o.QuestionId == question.Id).ToListAsync(cancellationToken);
        var ordered = Order(options, ShuffleOptions(question) ? matchQuestion.OptionOrderJson : null)
            .Select((o, i) => new CurrentQuestionOptionDto(o.Id, o.OptionText, i))
            .ToList();

        // The contract carries a single correct option; with several correct
        // options no one of them is "the" answer, so none is named.
        var correct = options.Where(o => o.IsCorrect).ToList();
        return new FormatContent(ordered, correct.Count == 1 ? correct[0].Id : null, null);
    }

    public virtual async Task<ResponseEvaluation> EvaluateAsync(
        Question question, IReadOnlyList<Guid>? selectedIds, string? freeText, CancellationToken cancellationToken)
    {
        if (selectedIds is null)
        {
            return ResponseEvaluation.NotChecked;
        }

        var options = await _db.QuestionOptions.Where(o => o.QuestionId == question.Id).ToListAsync(cancellationToken);
        if (options.Count == 0 || selectedIds.Any(id => options.All(o => o.Id != id)))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["selectedOptionId"] = ["A selected option does not belong to this question."],
            });
        }

        var correct = options.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();
        return new ResponseEvaluation(correct.SetEquals(selectedIds), true);
    }

    private static IEnumerable<QuestionOption> Order(IReadOnlyList<QuestionOption> options, string? optionOrderJson)
    {
        var order = string.IsNullOrWhiteSpace(optionOrderJson) ? null : JsonSerializer.Deserialize<List<Guid>>(optionOrderJson);
        if (order is null || order.Count == 0)
        {
            return options.OrderBy(o => o.DisplayOrder);
        }

        return options.OrderBy(o =>
        {
            var index = order.IndexOf(o.Id);
            return index < 0 ? int.MaxValue : index;
        });
    }
}
