using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;

namespace QuizApp.Application.Tournament.Commands;

public sealed record DeleteSegmentTemplateCommand(Guid ProgramId, Guid StageId, Guid SegmentTemplateId) : IRequest;

public sealed class DeleteSegmentTemplateCommandHandler : IRequestHandler<DeleteSegmentTemplateCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public DeleteSegmentTemplateCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteSegmentTemplateCommand request, CancellationToken cancellationToken)
    {
        var segment = await _db.StageSegmentTemplates
            .SingleOrDefaultAsync(
                s => s.Id == request.SegmentTemplateId && s.StageId == request.StageId && s.ProgramId == request.ProgramId,
                cancellationToken)
            ?? throw new KeyNotFoundException($"Segment '{request.SegmentTemplateId}' was not found.");

        segment.Delete(_currentUser.Email ?? "unknown");
        await _db.SaveChangesAsync(cancellationToken);
    }
}
