using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizware.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionDifficultyCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Question_Difficulty",
                table: "Questions",
                sql: "DifficultyLevel BETWEEN 1 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Question_Difficulty",
                table: "Questions");
        }
    }
}
