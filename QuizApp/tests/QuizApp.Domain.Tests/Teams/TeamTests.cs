using FluentAssertions;
using QuizApp.Domain.Enums;
using QuizApp.Domain.Teams;

namespace QuizApp.Domain.Tests.Teams;

public class TeamTests
{
    private static Team Register() =>
        Team.Register(Guid.NewGuid(), code: "T-01", schoolName: "Example School", displayName: "Example", createdBy: "owner");

    [Fact]
    public void Register_SetsRegisteredStatus()
    {
        var team = Register();

        team.Status.Should().Be(TeamStatus.Registered);
    }

    [Fact]
    public void ChangeStatus_WithReason_UpdatesStatusAndStampsReason()
    {
        var team = Register();

        team.ChangeStatus(TeamStatus.Withdrawn, "Team could not travel.", updatedBy: "admin");

        team.Status.Should().Be(TeamStatus.Withdrawn);
        team.StatusReason.Should().Be("Team could not travel.");
        team.StatusChangedAtUtc.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ChangeStatus_BlankReason_Throws(string reason)
    {
        var team = Register();

        var act = () => team.ChangeStatus(TeamStatus.Disqualified, reason, updatedBy: "admin");

        act.Should().Throw<ArgumentException>();
    }
}
