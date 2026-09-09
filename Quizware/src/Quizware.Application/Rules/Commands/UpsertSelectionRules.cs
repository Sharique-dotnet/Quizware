using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Rules.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Rules.Commands;

public sealed record UpsertSelectionRulesCommand(Guid ProgramId, IReadOnlyList<SelectionRuleAppDto> Rules) : IRequest<IReadOnlyList<SelectionRuleAppDto>>;

public sealed class UpsertSelectionRulesCommandValidator : AbstractValidator<UpsertSelectionRulesCommand>
{
    public UpsertSelectionRulesCommandValidator()
    {
        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.FormatCode).Must(f => Enum.TryParse<QuestionFormatCode>(f, ignoreCase: true, out _))
                .WithMessage("FormatCode is not a recognized question format.");
            rule.RuleFor(r => r.RepeatPolicy).Must(v => Enum.TryParse<RepeatPolicy>(v, ignoreCase: true, out _))
                .WithMessage("RepeatPolicy is not recognized.");
            rule.RuleFor(r => r.TopicSpreadPolicy).Must(v => Enum.TryParse<TopicSpreadPolicy>(v, ignoreCase: true, out _))
                .WithMessage("TopicSpreadPolicy is not recognized.");
            rule.RuleFor(r => r.FallbackPolicy).Must(v => Enum.TryParse<FallbackPolicy>(v, ignoreCase: true, out _))
                .WithMessage("FallbackPolicy is not recognized.");
        });
    }
}

public sealed class UpsertSelectionRulesCommandHandler : IRequestHandler<UpsertSelectionRulesCommand, IReadOnlyList<SelectionRuleAppDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpsertSelectionRulesCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<SelectionRuleAppDto>> Handle(UpsertSelectionRulesCommand request, CancellationToken cancellationToken)
    {
        var existing = await _db.QuestionSelectionRules
            .Where(r => r.ProgramId == request.ProgramId)
            .ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(r => r.Id);

        var actor = _currentUser.Email ?? "unknown";
        foreach (var dto in request.Rules)
        {
            var repeatPolicy = Enum.Parse<RepeatPolicy>(dto.RepeatPolicy, ignoreCase: true);
            var topicSpreadPolicy = Enum.Parse<TopicSpreadPolicy>(dto.TopicSpreadPolicy, ignoreCase: true);
            var fallbackPolicy = Enum.Parse<FallbackPolicy>(dto.FallbackPolicy, ignoreCase: true);

            if (dto.Id != Guid.Empty && existingById.TryGetValue(dto.Id, out var rule))
            {
                rule.Update(dto.DifficultyMixJson, repeatPolicy, topicSpreadPolicy, fallbackPolicy, actor, dto.TopicFilterJson);
                continue;
            }

            var formatCode = Enum.Parse<QuestionFormatCode>(dto.FormatCode, ignoreCase: true);
            var created = QuestionSelectionRule.Create(request.ProgramId, formatCode, actor, stageId: dto.StageId);
            created.Update(dto.DifficultyMixJson, repeatPolicy, topicSpreadPolicy, fallbackPolicy, actor, dto.TopicFilterJson);
            _db.QuestionSelectionRules.Add(created);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var all = await _db.QuestionSelectionRules
            .Where(r => r.ProgramId == request.ProgramId)
            .ToListAsync(cancellationToken);
        return all.Select(r => r.ToDto()).ToList();
    }
}
