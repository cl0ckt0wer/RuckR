using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuckR.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruitmentProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "Players",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PlayerEncounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerEncounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerEncounters_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserGameProfiles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Experience = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGameProfiles", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerEncounters_ExpiresAtUtc",
                table: "PlayerEncounters",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerEncounters_PlayerId",
                table: "PlayerEncounters",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerEncounters_UserId_PlayerId",
                table: "PlayerEncounters",
                columns: new[] { "UserId", "PlayerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerEncounters");

            migrationBuilder.DropTable(
                name: "UserGameProfiles");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Players");
        }
    }
}
