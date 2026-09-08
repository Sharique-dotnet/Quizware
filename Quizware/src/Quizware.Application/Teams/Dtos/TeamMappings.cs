using Quizware.Domain.Teams;

namespace Quizware.Application.Teams.Dtos;

internal static class TeamMappings
{
    public static TeamMemberDto ToDto(this TeamMember member) =>
        new(member.Id, member.FullName, member.RollNumber, member.Class, member.IsCaptain, member.PhotoUrl);

    public static TeamDto ToDto(this Team team, IReadOnlyList<TeamMember> members) => new(
        team.Id,
        team.Code,
        team.SchoolName,
        team.DisplayName,
        team.ShortName,
        team.ScoreImageUrl,
        team.SelectionImageUrl,
        team.ContactName,
        team.ContactPhone,
        team.ContactEmail,
        team.Status.ToString(),
        team.StatusReason,
        team.StatusChangedAtUtc,
        members.Select(m => m.ToDto()).ToList());

    public static TeamSummaryDto ToSummaryDto(this Team team) =>
        new(team.Id, team.Code, team.SchoolName, team.DisplayName, team.Status.ToString());
}
