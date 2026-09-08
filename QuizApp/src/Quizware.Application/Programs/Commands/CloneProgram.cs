using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Programs.Dtos;
using Quizware.Domain.Programs;

namespace Quizware.Application.Programs.Commands;

public sealed record CloneProgramCommand(Guid SourceProgramId, string NewProgramCode, string NewProgramName)
    : IRequest<ProgramDto>;

public sealed class CloneProgramCommandValidator : AbstractValidator<CloneProgramCommand>
{
    public CloneProgramCommandValidator()
    {
        RuleFor(x => x.SourceProgramId).NotEmpty();
        RuleFor(x => x.NewProgramCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.NewProgramName).NotEmpty().MaximumLength(200);
    }
}

/// <summary>Clones what Program-management itself owns — settings and
/// question-format enablement. Stages, segment templates and rule sets
/// belong to Phase 7's Tournament module, which has no Application handlers
/// yet; this handler is the extension point Phase 7 adds those copies to,
/// not a finished "clone everything" operation. The new program starts
/// Draft with zero teams/matches/scores by construction — those tables are
/// never touched here.</summary>
public sealed class CloneProgramCommandHandler : IRequestHandler<CloneProgramCommand, ProgramDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CloneProgramCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ProgramDto> Handle(CloneProgramCommand request, CancellationToken cancellationToken)
    {
        var source = await _db.Programs.SingleOrDefaultAsync(p => p.Id == request.SourceProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Program '{request.SourceProgramId}' was not found.");

        var actor = _currentUser.Email ?? "unknown";

        var clone = Program.Create(
            request.NewProgramCode,
            request.NewProgramName,
            actor,
            source.DefaultLanguage,
            source.TimeZoneId,
            source.Description);
        _db.Programs.Add(clone);

        var sourceSettings = await _db.ProgramSettings
            .Where(s => s.ProgramId == source.Id)
            .ToListAsync(cancellationToken);

        foreach (var setting in sourceSettings)
        {
            _db.ProgramSettings.Add(ProgramSetting.Create(
                clone.Id, setting.Category, setting.Key, setting.Value, setting.ValueType, actor, setting.Description));
        }

        var sourceFormats = await _db.ProgramQuestionFormats
            .Where(f => f.ProgramId == source.Id)
            .ToListAsync(cancellationToken);

        foreach (var format in sourceFormats)
        {
            var clonedFormat = ProgramQuestionFormat.Create(clone.Id, format.FormatCode, actor, format.DisplayOrder);
            if (!format.IsEnabled)
            {
                clonedFormat.Disable(format.DisabledReason ?? "Cloned as disabled.", actor);
            }

            _db.ProgramQuestionFormats.Add(clonedFormat);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return clone.ToDto();
    }
}
