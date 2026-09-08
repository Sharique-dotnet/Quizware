using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizware.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixMatchParticipantRemovalCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MP_Removal",
                table: "MatchParticipants");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MP_Removal",
                table: "MatchParticipants",
                sql: "Status = 0 OR (RemovalReason IS NOT NULL AND RemovedAtUtc IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MP_Removal",
                table: "MatchParticipants");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MP_Removal",
                table: "MatchParticipants",
                sql: "Status = 1 OR (RemovalReason IS NOT NULL AND RemovedAtUtc IS NOT NULL)");
        }
    }
}
