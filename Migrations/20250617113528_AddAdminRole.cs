using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppGambit.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Comments_ApplicationId_CreatedAt",
                table: "Comments",
                columns: new[] { "ApplicationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_CreatedAt",
                table: "Comments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Category",
                table: "Applications",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CreatedAt",
                table: "Applications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CreatedAt_Category",
                table: "Applications",
                columns: new[] { "CreatedAt", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Name",
                table: "Applications",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Name_Category",
                table: "Applications",
                columns: new[] { "Name", "Category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comments_ApplicationId_CreatedAt",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_CreatedAt",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Applications_Category",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_CreatedAt",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_CreatedAt_Category",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_Name",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_Name_Category",
                table: "Applications");
        }
    }
}
