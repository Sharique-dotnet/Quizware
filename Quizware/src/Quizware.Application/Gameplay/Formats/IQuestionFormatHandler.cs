using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>What the live screen shows for a question. The contract has one
/// media URL and a flat option list.</summary>
public sealed record FormatContent(IReadOnlyList<CurrentQuestionOptionDto> Options, Guid? CorrectOptionId, string? MediaUrl);

/// <summary>An objective check of a team's response. <see cref="IsObjective"/>
/// means the recorded outcome must agree with <see cref="IsCorrect"/>; a
/// non-objective result (typed answers) never overrules the operator.</summary>
public sealed record ResponseEvaluation(bool? IsCorrect, bool IsObjective)
{
    public static readonly ResponseEvaluation NotChecked = new(null, false);
}

/// <summary>How a question may be passed on: at most <see cref="MaxPassCount"/>
/// times, in <see cref="Direction"/>, and whether the answer is revealed when
/// every team has passed.</summary>
public sealed record QuestionPassRules(int MaxPassCount, PassDirection Direction, bool RevealAnswerIfAllPass);

/// <summary>How a question appears on a topic-pick board: its label, whether
/// picking it removes every other question with that label from the board,
/// and its position on the board.</summary>
public sealed record TopicChoice(string Label, bool IsExclusive, int? DisplayOrder);

/// <summary>P9-07: everything that differs between formats on the night. The
/// engine asks the handler for the question's format and is otherwise
/// format-agnostic. Handlers are discovered by assembly scanning, so adding a
/// format means adding one class — no existing code changes.</summary>
public interface IQuestionFormatHandler
{
    QuestionFormatCode Format { get; }

    /// <summary>BR-2.4: in these formats nobody holds the question — any
    /// active team may answer.</summary>
    bool AnyTeamMayAnswer { get; }

    /// <summary>The format's own timer, used when neither the question nor
    /// the segment sets one.</summary>
    int? DefaultTimeLimitSeconds(Question question);

    /// <summary>For formats any team may answer: whether a wrong answer
    /// leaves the question open for the others.</summary>
    bool WrongAnswerLeavesQuestionOpen(Question question);

    /// <summary>The question's own passing rules, for formats that carry
    /// them; null means the segment's passing settings apply.</summary>
    QuestionPassRules? PassRules(Question question);

    /// <summary>The question's own topic-board entry, for formats that carry
    /// one; null means the question's Topic name is used.</summary>
    TopicChoice? TopicChoice(Question question);

    Task<FormatContent> PresentAsync(Question question, MatchQuestion matchQuestion, CancellationToken cancellationToken);

    Task<ResponseEvaluation> EvaluateAsync(
        Question question, IReadOnlyList<Guid>? selectedIds, string? freeText, CancellationToken cancellationToken);
}
