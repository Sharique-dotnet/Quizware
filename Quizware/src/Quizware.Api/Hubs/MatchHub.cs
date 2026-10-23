using MediatR;
using Microsoft.AspNetCore.Authorization;
using Quizware.Application.Authorization;
using Quizware.Application.Gameplay;

namespace Quizware.Api.Hubs;

/// <summary>P12-01: <c>/hubs/match</c> — operators, scorers and auditors. Each
/// sees the state its role allows on the REST API (operators and scorers see
/// the answer as soon as a question is served).</summary>
[Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin},{Roles.Operator},{Roles.Scorer},{Roles.Auditor}")]
public sealed class MatchHub : LiveHubBase
{
    public const string Path = "/hubs/match";

    public MatchHub(ISender sender)
        : base(sender)
    {
    }

    protected override LiveAudience? Audience => null;
}
