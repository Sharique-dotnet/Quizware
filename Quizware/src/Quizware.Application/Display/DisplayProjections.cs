using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Application.Scoring;
using Quizware.Domain.Enums;

namespace Quizware.Application.Display;

public sealed record DisplayMatchStateDto(LiveMatchStateDto Live, bool AnswerRevealed);

public sealed record DisplayScoreDto(Guid TeamId, string TeamName, int Score);

public sealed record DisplayStandingDto(Guid TeamId, string TeamName, int Score, int Rank);

public sealed record DisplayBrandingDto(string? LogoUrl, string? PrimaryColor, string? SecondaryColor, string? FontFamily);

/// <summary>P12-07: the read-only payloads the projector screens show. A
/// display never sees an answer before it is revealed — the match state is
/// always built for the Display audience, whoever asks.</summary>
public interface IDisplayProjectionService
{
    Task<DisplayMatchStateDto> MatchStateAsync(Guid matchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DisplayScoreDto>> ScoresAsync(Guid matchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DisplayStandingDto>> StandingsAsync(Guid programId, CancellationToken cancellationToken);

    Task<DisplayBrandingDto> BrandingAsync(Guid programId, CancellationToken cancellationToken);
}

public sealed class DisplayProjectionService : IDisplayProjectionService
{
    private readonly IAppDbContext _db;
    private readonly LiveStateBuilder _state;
    private readonly Standings _standings;

    public DisplayProjectionService(IAppDbContext db, LiveStateBuilder state, Standings standings)
    {
        _db = db;
        _state = state;
        _standings = standings;
    }

    public async Task<DisplayMatchStateDto> MatchStateAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, matchId, cancellationToken);
        var live = await _state.BuildAsync(match, LiveAudience.Display, cancellationToken);
        var revealed = await _db.MatchQuestions.AnyAsync(
            q => q.MatchId == matchId && q.State == MatchQuestionState.Active && q.RevealedAtUtc != null, cancellationToken);
        return new DisplayMatchStateDto(live, revealed);
    }

    public async Task<IReadOnlyList<DisplayScoreDto>> ScoresAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await MatchScoreboard.LoadMatchAsync(_db, matchId, cancellationToken);
        return (await MatchScoreboard.BuildAsync(_db, match, cancellationToken))
            .Select(s => new DisplayScoreDto(s.TeamId, s.TeamName, s.Score))
            .ToList();
    }

    public async Task<IReadOnlyList<DisplayStandingDto>> StandingsAsync(Guid programId, CancellationToken cancellationToken) =>
        (await _standings.OverallAsync(programId, cancellationToken))
            .Select(s => new DisplayStandingDto(s.TeamId, s.TeamName, s.Score, s.Rank))
            .ToList();

    public async Task<DisplayBrandingDto> BrandingAsync(Guid programId, CancellationToken cancellationToken)
    {
        var program = await _db.Programs.SingleOrDefaultAsync(p => p.Id == programId, cancellationToken)
            ?? throw new KeyNotFoundException($"Program '{programId}' was not found.");
        return new DisplayBrandingDto(program.LogoUrl, program.ThemePrimaryColor, program.ThemeSecondaryColor, program.FontFamily);
    }
}

public sealed record GetDisplayMatchStateQuery(Guid MatchId) : IRequest<DisplayMatchStateDto>;

public sealed record GetDisplayScoresQuery(Guid MatchId) : IRequest<IReadOnlyList<DisplayScoreDto>>;

public sealed record GetDisplayStandingsQuery(Guid ProgramId) : IRequest<IReadOnlyList<DisplayStandingDto>>;

public sealed record GetDisplayBrandingQuery(Guid ProgramId) : IRequest<DisplayBrandingDto>;

public sealed class DisplayQueriesHandler :
    IRequestHandler<GetDisplayMatchStateQuery, DisplayMatchStateDto>,
    IRequestHandler<GetDisplayScoresQuery, IReadOnlyList<DisplayScoreDto>>,
    IRequestHandler<GetDisplayStandingsQuery, IReadOnlyList<DisplayStandingDto>>,
    IRequestHandler<GetDisplayBrandingQuery, DisplayBrandingDto>
{
    private readonly IDisplayProjectionService _display;

    public DisplayQueriesHandler(IDisplayProjectionService display)
    {
        _display = display;
    }

    public Task<DisplayMatchStateDto> Handle(GetDisplayMatchStateQuery request, CancellationToken cancellationToken) =>
        _display.MatchStateAsync(request.MatchId, cancellationToken);

    public Task<IReadOnlyList<DisplayScoreDto>> Handle(GetDisplayScoresQuery request, CancellationToken cancellationToken) =>
        _display.ScoresAsync(request.MatchId, cancellationToken);

    public Task<IReadOnlyList<DisplayStandingDto>> Handle(GetDisplayStandingsQuery request, CancellationToken cancellationToken) =>
        _display.StandingsAsync(request.ProgramId, cancellationToken);

    public Task<DisplayBrandingDto> Handle(GetDisplayBrandingQuery request, CancellationToken cancellationToken) =>
        _display.BrandingAsync(request.ProgramId, cancellationToken);
}
