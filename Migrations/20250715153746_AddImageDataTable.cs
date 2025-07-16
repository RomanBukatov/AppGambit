using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AppGambit.Migrations
{
    /// <inheritdoc />
    public partial class AddImageDataTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProfileImageId",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IconImageId",
                table: "Applications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    ApplicationId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Images_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ProfileImageId",
                table: "AspNetUsers",
                column: "ProfileImageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_IconImageId",
                table: "Applications",
                column: "IconImageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_ApplicationId",
                table: "Images",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_CreatedAt",
                table: "Images",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Images_Type",
                table: "Images",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Images_UserId",
                table: "Images",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Images_IconImageId",
                table: "Applications",
                column: "IconImageId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Images_ProfileImageId",
                table: "AspNetUsers",
                column: "ProfileImageId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Images_IconImageId",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Images_ProfileImageId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ProfileImageId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_Applications_IconImageId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ProfileImageId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IconImageId",
                table: "Applications");
        }
    }
}
