using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Application.Gameplay.Formats;

namespace Quizware.Application.Gameplay.Queries;

public sealed record GetAvailableTopicsQuery(Guid MatchId) : IRequest<AvailableTopicsDto>;

/// <summary>The board: the distinct topics still on offer in the open segment,
/// in board order (Choice's TopicDisplayOrder, then name). Empty when no
/// segment is open or the open one does not use topic picks.</summary>
public sealed class GetAvailableTopicsQueryHandler : IRequestHandler<GetAvailableTopicsQuery, AvailableTopicsDto>
{
    private readonly IAppDbContext _db;
    private readonly QuestionFormatHandlers _formats;

    public GetAvailableTopicsQueryHandler(IAppDbContext db, QuestionFormatHandlers formats)
    {
        _db = db;
        _formats = formats;
    }

    public async Task<AvailableTopicsDto> Handle(GetAvailableTopicsQuery request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        var open = await TopicPicks.OpenSegmentAsync(_db, match.Id, cancellationToken);
        if (open is not { Template.TopicSelectionMode: Domain.Enums.TopicSelectionMode.TeamPicksTopic } segment)
        {
            return new AvailableTopicsDto([], null);
        }

        var candidates = await TopicPicks.CandidatesAsync(_db, _formats, segment.Segment.Id, cancellationToken);
        var topics = candidates
            .GroupBy(c => c.TopicName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Min(c => c.DisplayOrder ?? int.MaxValue))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Key)
            .ToList();
        return new AvailableTopicsDto(topics, segment.Template!.TopicChoiceLimit);
    }
}
