using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedmanga.Migrations
{
    /// <inheritdoc />
    public partial class UpdateChapterUniqueIndex_FilterDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Chapters_StoryId_ChapterNumber",
                table: "Chapters");

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_StoryId_ChapterNumber",
                table: "Chapters",
                columns: new[] { "StoryId", "ChapterNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Chapters_StoryId_ChapterNumber",
                table: "Chapters");

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_StoryId_ChapterNumber",
                table: "Chapters",
                columns: new[] { "StoryId", "ChapterNumber" },
                unique: true);
        }
    }
}
