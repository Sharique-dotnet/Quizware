using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Programs.Dtos;

namespace QuizApp.Application.Programs.Commands;

public sealed record UpdateProgramCommand(
    Guid Id,
    string Name,
    string? Description,
    string? OrganisationName,
    string? LogoUrl,
    string? ThemePrimaryColor,
    string? ThemeSecondaryColor,
    string? FontFamily) : IRequest<ProgramDto>;

public sealed class UpdateProgramCommandValidator : AbstractValidator<UpdateProgramCommand>
{
    public UpdateProgramCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateProgramCommandHandler : IRequestHandler<UpdateProgramCommand, ProgramDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateProgramCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ProgramDto> Handle(UpdateProgramCommand request, CancellationToken cancellationToken)
    {
        var program = await _db.Programs.SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Program '{request.Id}' was not found.");

        program.UpdateDetails(
            request.Name,
            request.Description,
            request.OrganisationName,
            request.LogoUrl,
            request.ThemePrimaryColor,
            request.ThemeSecondaryColor,
            request.FontFamily,
            _currentUser.Email ?? "unknown");

        await _db.SaveChangesAsync(cancellationToken);

        return program.ToDto();
    }
}
