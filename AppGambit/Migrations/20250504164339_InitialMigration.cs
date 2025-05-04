using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppGambit.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ratings_UserId_CommentId",
                table: "ratings");

            migrationBuilder.DropIndex(
                name: "IX_ratings_UserId_ProgramId",
                table: "ratings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rating_Target",
                table: "ratings");

            migrationBuilder.UpdateData(
                table: "about_creator_info",
                keyColumn: "InfoId",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTime(2025, 5, 4, 16, 43, 38, 573, DateTimeKind.Utc).AddTicks(932));

            migrationBuilder.CreateIndex(
                name: "IX_ratings_UserId_CommentId",
                table: "ratings",
                columns: new[] { "UserId", "CommentId" },
                unique: true,
                filter: "\"ProgramId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ratings_UserId_ProgramId",
                table: "ratings",
                columns: new[] { "UserId", "ProgramId" },
                unique: true,
                filter: "\"CommentId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rating_Target",
                table: "ratings",
                sql: "(\"ProgramId\" IS NOT NULL AND \"CommentId\" IS NULL) OR (\"ProgramId\" IS NULL AND \"CommentId\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ratings_UserId_CommentId",
                table: "ratings");

            migrationBuilder.DropIndex(
                name: "IX_ratings_UserId_ProgramId",
                table: "ratings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rating_Target",
                table: "ratings");

            migrationBuilder.UpdateData(
                table: "about_creator_info",
                keyColumn: "InfoId",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTime(2025, 5, 4, 16, 7, 22, 106, DateTimeKind.Utc).AddTicks(6216));

            migrationBuilder.CreateIndex(
                name: "IX_ratings_UserId_CommentId",
                table: "ratings",
                columns: new[] { "UserId", "CommentId" },
                unique: true,
                filter: "program_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ratings_UserId_ProgramId",
                table: "ratings",
                columns: new[] { "UserId", "ProgramId" },
                unique: true,
                filter: "comment_id IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rating_Target",
                table: "ratings",
                sql: "(program_id IS NOT NULL AND comment_id IS NULL) OR (program_id IS NULL AND comment_id IS NOT NULL)");
        }
    }
}
