using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedmanga.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminStoredProceduresAndTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    LogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminUserId = table.Column<int>(type: "int", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.LogId);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_AdminUserId",
                table: "AuditLogs",
                column: "AdminUserId");

            // Custom Stored Procedures & Triggers
            var sqlFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Migrations", "AdminSPs.sql");
            if (System.IO.File.Exists(sqlFile))
            {
                migrationBuilder.Sql(System.IO.File.ReadAllText(sqlFile));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_Admin_CreateStory;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_Admin_UpdateStory;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_Admin_DeleteStory;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_Admin_CreateChapter;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_Admin_UpdateChapter;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_Admin_DeleteChapter;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_Admin_CreateCategory;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_Admin_UpdateCategory;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_Admin_DeleteCategory;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_Admin_UpdateUserStatus;");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TRG_UpdateStoryRating;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TRG_UpdateStoryViewCount;");
        }
    }
}
