using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Programs.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Application.Programs.Commands;

public sealed record ProgramFormatEntryCommand(QuestionFormatCode FormatCode, bool IsEnabled, string? DisabledReason);

public sealed record UpdateProgramFormatsCommand(Guid ProgramId, IReadOnlyList<ProgramFormatEntryCommand> Formats)
    : IRequest<IReadOnlyList<ProgramFormatDto>>;

public sealed class UpdateProgramFormatsCommandValidator : AbstractValidator<UpdateProgramFormatsCommand>
{
    public UpdateProgramFormatsCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleFor(x => x.Formats).NotEmpty();
        RuleForEach(x => x.Formats)
            .ChildRules(f => f.RuleFor(e => e.DisabledReason).NotEmpty().When(e => !e.IsEnabled));
    }
}

/// <summary>Disabling a format still referenced by a stage segment template
/// is rejected with <see cref="FormatInUseException"/> — never silently
/// destructive (05-API-Design.md §5.4).</summary>
public sealed class UpdateProgramFormatsCommandHandler
    : IRequestHandler<UpdateProgramFormatsCommand, IReadOnlyList<ProgramFormatDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateProgramFormatsCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProgramFormatDto>> Handle(
        UpdateProgramFormatsCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        var actor = _currentUser.Email ?? "unknown";

        var existing = await _db.ProgramQuestionFormats
            .Where(f => f.ProgramId == request.ProgramId)
            .ToListAsync(cancellationToken);

        foreach (var entry in request.Formats)
        {
            var format = existing.SingleOrDefault(f => f.FormatCode == entry.FormatCode)
                ?? throw new KeyNotFoundException(
                    $"Format '{entry.FormatCode}' is not registered for program '{request.ProgramId}'.");

            if (entry.IsEnabled)
            {
                format.Enable(actor);
                continue;
            }

            var usedBy = await _db.StageSegmentTemplates
                .Where(t => t.ProgramId == request.ProgramId && t.FormatCode == entry.FormatCode)
                .Join(_db.Stages, t => t.StageId, s => s.Id, (t, s) => new FormatUsage(s.Id, s.Name, t.Id))
                .ToListAsync(cancellationToken);

            if (usedBy.Count > 0)
            {
                throw new FormatInUseException(entry.FormatCode.ToString(), usedBy);
            }

            format.Disable(entry.DisabledReason!, actor);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return existing.Select(f => f.ToDto()).ToList();
    }
}
