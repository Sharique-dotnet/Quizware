using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace QuizApp.Modules.Buzzer.Manual;

public sealed class BuzzDeviceMappingConfiguration : IEntityTypeConfiguration<BuzzDeviceMapping>
{
    public void Configure(EntityTypeBuilder<BuzzDeviceMapping> builder)
    {
        builder.HasKey(b => b.Id);
        builder.HasIndex(b => new { b.MatchId, b.DeviceId }).IsUnique().HasFilter("IsDeleted = 0");
    }
}

public sealed class BuzzSessionConfiguration : IEntityTypeConfiguration<BuzzSession>
{
    public void Configure(EntityTypeBuilder<BuzzSession> builder)
    {
        builder.HasKey(b => b.Id);
        builder.HasIndex(b => new { b.MatchId, b.State });
    }
}

public sealed class BuzzPressConfiguration : IEntityTypeConfiguration<BuzzPress>
{
    public void Configure(EntityTypeBuilder<BuzzPress> builder)
    {
        builder.HasKey(b => b.Id);
        builder.HasIndex(b => new { b.BuzzSessionId, b.Rank });
    }
}
