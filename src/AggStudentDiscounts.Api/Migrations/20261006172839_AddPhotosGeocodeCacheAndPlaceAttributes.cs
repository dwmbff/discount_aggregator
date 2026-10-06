using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AggStudentDiscounts.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotosGeocodeCacheAndPlaceAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_UserId",
                table: "Applications");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Applications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Applications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Applications",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkingHours",
                table: "Applications",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StoredName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationPhotos_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GeocodeCache",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeocodeCache", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_UserId_CreatedAt",
                table: "Applications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPhotos_ApplicationId",
                table: "ApplicationPhotos",
                column: "ApplicationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationPhotos");

            migrationBuilder.DropTable(
                name: "GeocodeCache");

            migrationBuilder.DropIndex(
                name: "IX_Applications_UserId_CreatedAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "WorkingHours",
                table: "Applications");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_UserId",
                table: "Applications",
                column: "UserId");
        }
    }
}
