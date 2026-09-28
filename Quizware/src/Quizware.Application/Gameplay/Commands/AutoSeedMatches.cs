using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Scoring;
using Quizware.Domain.Enums;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay.Commands;

public sealed record AutoSeedMatchesCommand(Guid ProgramId, Guid StageId, string SeedingMode) : IRequest<int>;

public sealed class AutoSeedMatchesCommandValidator : AbstractValidator<AutoSeedMatchesCommand>
{
    private static readonly SeedingMode[] Automatic = [SeedingMode.Random, SeedingMode.ByRank, SeedingMode.Snake];

    public AutoSeedMatchesCommandValidator()
    {
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.SeedingMode)
            .Must(m => Enum.TryParse<SeedingMode>(m, ignoreCase: true, out var mode) && Automatic.Contains(mode))
            .WithMessage($"SeedingMode must be one of: {string.Join(", ", Automatic)}.");
    }
}

/// <summary>P9-01: creates the stage's matches automatically. The teams are
/// those committed to this stage by qualification when there are any,
/// otherwise every registered or active team in the program; teams already in
/// one of the stage's matches are left out. Ranking — for ByRank and Snake —
/// is the qualification rank, or failing that the program's overall standing,
/// then the team's sort order. Each match is created exactly as a manual one
/// (segments from the stage templates), with seats and turn order in group
/// order. Returns the number of matches created.</summary>
public sealed class AutoSeedMatchesCommandHandler : IRequestHandler<AutoSeedMatchesCommand, int>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly Standings _standings;

    public AutoSeedMatchesCommandHandler(IAppDbContext db, ICurrentUser currentUser, Standings standings)
    {
        _db = db;
        _currentUser = currentUser;
        _standings = standings;
    }

    public async Task<int> Handle(AutoSeedMatchesCommand request, CancellationToken cancellationToken)
    {
        var stage = await _db.Stages
            .SingleOrDefaultAsync(s => s.Id == request.StageId && s.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage '{request.StageId}' was not found.");
        var mode = Enum.Parse<SeedingMode>(request.SeedingMode, ignoreCase: true);
        var actor = _currentUser.Email ?? "unknown";

        var ranked = await RankedPoolAsync(request.ProgramId, stage.Id, cancellationToken);
        var groups = MatchSeeding.Group(
            ranked, Math.Max(2, stage.MinTeamsPerMatch), stage.MaxTeamsPerMatch, mode, new Random(Random.Shared.Next()));

        var templates = await _db.StageSegmentTemplates.Where(t => t.StageId == stage.Id).ToListAsync(cancellationToken);
        var nextNumber = (await _db.Matches.Where(m => m.StageId == stage.Id).Select(m => (int?)m.MatchNumber).MaxAsync(cancellationToken) ?? 0) + 1;

        foreach (var group in groups)
        {
            var match = Match.Create(
                request.ProgramId, stage.Id, nextNumber, Random.Shared.NextInt64(1, long.MaxValue), actor, name: $"Match {nextNumber}");
            nextNumber++;
            _db.Matches.Add(match);
            _db.MatchSegments.AddRange(CreateMatchCommandHandler.InstantiateSegments(match, stage, templates, actor));
            for (var i = 0; i < group.Count; i++)
            {
                _db.MatchParticipants.Add(MatchParticipant.Create(request.ProgramId, match.Id, group[i], i + 1, i + 1, actor));
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return groups.Count;
    }

    private async Task<List<Guid>> RankedPoolAsync(Guid programId, Guid stageId, CancellationToken cancellationToken)
    {
        var alreadyPlaced = await (
            from participant in _db.MatchParticipants
            join match in _db.Matches on participant.MatchId equals match.Id
            where match.StageId == stageId
            select participant.TeamId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var qualified = await _db.StageQualifications
            .Where(q => q.ToStageId == stageId && q.IsCommitted)
            .OrderBy(q => q.StageRank)
            .Select(q => q.TeamId)
            .ToListAsync(cancellationToken);
        if (qualified.Count > 0)
        {
            return qualified.Where(id => !alreadyPlaced.Contains(id)).Distinct().ToList();
        }

        var teams = await _db.Teams
            .Where(t => t.ProgramId == programId && (t.Status == TeamStatus.Registered || t.Status == TeamStatus.Active))
            .Select(t => new { t.Id, t.SortOrder, t.DisplayName })
            .ToListAsync(cancellationToken);
        var overall = (await _standings.OverallAsync(programId, cancellationToken)).ToDictionary(s => s.TeamId, s => s.Rank);

        return teams
            .Where(t => !alreadyPlaced.Contains(t.Id))
            .OrderBy(t => overall.GetValueOrDefault(t.Id, int.MaxValue))
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.DisplayName)
            .Select(t => t.Id)
            .ToList();
    }
}
