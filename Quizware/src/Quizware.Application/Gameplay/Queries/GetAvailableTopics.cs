using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Queries;

public sealed record GetAvailableTopicsQuery(Guid MatchId) : IRequest<AvailableTopicsDto>;

/// <summary>The distinct topics still on offer in the open segment. Empty when
/// no segment is open or the open one does not use topic picks.</summary>
public sealed class GetAvailableTopicsQueryHandler : IRequestHandler<GetAvailableTopicsQuery, AvailableTopicsDto>
{
    private readonly IAppDbContext _db;

    public GetAvailableTopicsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<AvailableTopicsDto> Handle(GetAvailableTopicsQuery request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        var open = await TopicPicks.OpenSegmentAsync(_db, match.Id, cancellationToken);
        if (open is not { Template.TopicSelectionMode: Domain.Enums.TopicSelectionMode.TeamPicksTopic } segment)
        {
            return new AvailableTopicsDto([], null);
        }

        var candidates = await TopicPicks.CandidatesAsync(_db, segment.Segment.Id, cancellationToken);
        var topics = candidates
            .Select(c => c.TopicName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new AvailableTopicsDto(topics, segment.Template!.TopicChoiceLimit);
    }
}
