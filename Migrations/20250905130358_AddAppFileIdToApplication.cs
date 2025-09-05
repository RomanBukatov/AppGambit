using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppGambit.Migrations
{
    /// <inheritdoc />
    public partial class AddAppFileIdToApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppFileId",
                table: "Applications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_AppFileId",
                table: "Applications",
                column: "AppFileId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Images_AppFileId",
                table: "Applications",
                column: "AppFileId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Images_AppFileId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_AppFileId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "AppFileId",
                table: "Applications");
        }
    }
}
