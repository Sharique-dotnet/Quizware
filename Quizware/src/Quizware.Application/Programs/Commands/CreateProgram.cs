using FluentValidation;
using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Programs.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Programs;

namespace Quizware.Application.Programs.Commands;

public sealed record CreateProgramCommand(string Code, string Name, string? Description) : IRequest<ProgramDto>;

public sealed class CreateProgramCommandValidator : AbstractValidator<CreateProgramCommand>
{
    public CreateProgramCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

/// <summary>Creates the program and, per P6-01's acceptance criterion, seeds
/// one enabled <see cref="ProgramQuestionFormat"/> row for every format
/// code — no format is compulsory, but every program starts with all of
/// them available.</summary>
public sealed class CreateProgramCommandHandler : IRequestHandler<CreateProgramCommand, ProgramDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateProgramCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ProgramDto> Handle(CreateProgramCommand request, CancellationToken cancellationToken)
    {
        var actor = _currentUser.Email ?? "unknown";
        var program = Program.Create(request.Code, request.Name, actor, description: request.Description);
        _db.Programs.Add(program);

        foreach (var formatCode in Enum.GetValues<QuestionFormatCode>())
        {
            _db.ProgramQuestionFormats.Add(ProgramQuestionFormat.Create(program.Id, formatCode, actor));
        }

        await _db.SaveChangesAsync(cancellationToken);

        return program.ToDto();
    }
}
