using FluentAssertions;
using Quizware.Domain.Enums;
using Quizware.Domain.Qualification;

namespace Quizware.Domain.Tests.Qualification;

public class StageQualificationTests
{
    private static StageQualification Create() =>
        StageQualification.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), QualificationReason.Manual, 90, 11, "admin");

    [Fact]
    public void CommitThenUncommit_LeavesItUncommitted()
    {
        var qualification = Create();
        qualification.Commit();

        qualification.Uncommit("admin");

        qualification.IsCommitted.Should().BeFalse();
        qualification.UpdatedBy.Should().Be("admin");
    }

    [Fact]
    public void Delete_SoftDeletes()
    {
        var qualification = Create();

        qualification.Delete("admin");

        qualification.IsDeleted.Should().BeTrue();
        qualification.DeletedAtUtc.Should().NotBeNull();
    }
}

public class TieBreakParticipantTests
{
    [Fact]
    public void ANewParticipant_IsContested_UntilItsResultIsRecorded()
    {
        var participant = TieBreakParticipant.Create(Guid.NewGuid(), Guid.NewGuid(), enteringScore: 110, "system");
        participant.IsContested.Should().BeTrue();

        participant.RecordPoints(20);
        participant.IsContested.Should().BeTrue();
        participant.TieBreakPoints.Should().Be(20);

        participant.RecordResult(30, resultRank: 1, qualified: true);
        participant.IsContested.Should().BeFalse();
        participant.Qualified.Should().BeTrue();
    }
}
