using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Programs.Dtos;
using Quizware.Domain.Programs;

namespace Quizware.Application.Programs.Commands;

public sealed record ProgramSettingEntryCommand(string Category, string Key, string Value);

public sealed record UpdateProgramSettingsCommand(Guid ProgramId, IReadOnlyList<ProgramSettingEntryCommand> Settings)
    : IRequest<IReadOnlyList<ProgramSettingDto>>;

public sealed class UpdateProgramSettingsCommandValidator : AbstractValidator<UpdateProgramSettingsCommand>
{
    public UpdateProgramSettingsCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleForEach(x => x.Settings).ChildRules(s =>
        {
            s.RuleFor(e => e.Category).NotEmpty().MaximumLength(100);
            s.RuleFor(e => e.Key).NotEmpty().MaximumLength(100);
        });
    }
}

/// <summary>Upserts each entry: updates the value in place if a row for
/// (ProgramId, Category, Key) already exists, otherwise creates it.</summary>
public sealed class UpdateProgramSettingsCommandHandler
    : IRequestHandler<UpdateProgramSettingsCommand, IReadOnlyList<ProgramSettingDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateProgramSettingsCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProgramSettingDto>> Handle(
        UpdateProgramSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        var actor = _currentUser.Email ?? "unknown";

        var existing = await _db.ProgramSettings
            .Where(s => s.ProgramId == request.ProgramId)
            .ToListAsync(cancellationToken);

        foreach (var entry in request.Settings)
        {
            var match = existing.SingleOrDefault(s => s.Category == entry.Category && s.Key == entry.Key);
            if (match is not null)
            {
                match.UpdateValue(entry.Value, actor);
            }
            else
            {
                _db.ProgramSettings.Add(
                    ProgramSetting.Create(request.ProgramId, entry.Category, entry.Key, entry.Value, "string", actor));
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var all = await _db.ProgramSettings
            .Where(s => s.ProgramId == request.ProgramId)
            .ToListAsync(cancellationToken);

        return all.Select(s => s.ToDto()).ToList();
    }
}
