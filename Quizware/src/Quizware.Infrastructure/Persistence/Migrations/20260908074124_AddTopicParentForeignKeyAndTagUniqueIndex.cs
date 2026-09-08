using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizware.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicParentForeignKeyAndTagUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Topics_ParentTopicId",
                table: "Topics",
                column: "ParentTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_ProgramId_Name",
                table: "Tags",
                columns: new[] { "ProgramId", "Name" },
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_Topics_ParentTopicId",
                table: "Topics",
                column: "ParentTopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Topics_ParentTopicId",
                table: "Topics");

            migrationBuilder.DropIndex(
                name: "IX_Topics_ParentTopicId",
                table: "Topics");

            migrationBuilder.DropIndex(
                name: "IX_Tags_ProgramId_Name",
                table: "Tags");
        }
    }
}
