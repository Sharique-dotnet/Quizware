using MediatR;
using Microsoft.AspNetCore.Authorization;
using Quizware.Application.Authorization;
using Quizware.Application.Gameplay;

namespace Quizware.Api.Hubs;

/// <summary>P12-02/04: <c>/hubs/display</c> — projector screens, typically on a
/// read-only display token. Always the Display view, whoever connects: the
/// correct answer is withheld until the question is revealed. It has no
/// method that writes anything.</summary>
[Authorize(Policy = Policies.CanViewLive)]
public sealed class DisplayHub : LiveHubBase
{
    public const string Path = "/hubs/display";

    public DisplayHub(ISender sender)
        : base(sender)
    {
    }

    protected override LiveAudience? Audience => LiveAudience.Display;
}
