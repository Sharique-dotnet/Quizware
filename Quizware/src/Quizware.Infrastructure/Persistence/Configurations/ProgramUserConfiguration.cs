using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Quizware.Infrastructure.Identity;

namespace Quizware.Infrastructure.Persistence.Configurations;

public sealed class ProgramUserConfiguration : IEntityTypeConfiguration<ProgramUser>
{
    public void Configure(EntityTypeBuilder<ProgramUser> builder)
    {
        builder.HasKey(pu => pu.Id);
        builder.HasIndex(pu => new { pu.ProgramId, pu.UserId, pu.RoleId }).IsUnique().HasFilter("IsDeleted = 0");
    }
}
