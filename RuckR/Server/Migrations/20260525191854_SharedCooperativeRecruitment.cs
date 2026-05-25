using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuckR.Server.Migrations
{
    /// <inheritdoc />
    public partial class SharedCooperativeRecruitment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PlayerEncounters",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "AreaKey",
                table: "PlayerEncounters",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParkPlaceId",
                table: "PlayerEncounters",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecruitmentParticipants",
                columns: table => new
                {
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    AccuracyMeters = table.Column<double>(type: "float", nullable: true),
                    CollectionAwardedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecruitmentParticipants", x => new { x.EncounterId, x.UserId });
                    table.ForeignKey(
                        name: "FK_RecruitmentParticipants_PlayerEncounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "PlayerEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO RecruitmentParticipants (EncounterId, UserId, JoinedAtUtc, Latitude, Longitude, AccuracyMeters, CollectionAwardedAtUtc)
                SELECT Id, UserId, CreatedAtUtc, Latitude, Longitude, NULL, NULL
                FROM PlayerEncounters
                WHERE UserId IS NOT NULL
                    AND RecruitmentStartedAtUtc IS NOT NULL
                    AND RecruitmentCompletesAtUtc IS NOT NULL
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerEncounters_AreaKey_ExpiresAtUtc",
                table: "PlayerEncounters",
                columns: new[] { "AreaKey", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerEncounters_AreaKey_PlayerId",
                table: "PlayerEncounters",
                columns: new[] { "AreaKey", "PlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentParticipants_CollectionAwardedAtUtc",
                table: "RecruitmentParticipants",
                column: "CollectionAwardedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentParticipants_UserId",
                table: "RecruitmentParticipants",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecruitmentParticipants");

            migrationBuilder.DropIndex(
                name: "IX_PlayerEncounters_AreaKey_ExpiresAtUtc",
                table: "PlayerEncounters");

            migrationBuilder.DropIndex(
                name: "IX_PlayerEncounters_AreaKey_PlayerId",
                table: "PlayerEncounters");

            migrationBuilder.DropColumn(
                name: "AreaKey",
                table: "PlayerEncounters");

            migrationBuilder.DropColumn(
                name: "ParkPlaceId",
                table: "PlayerEncounters");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PlayerEncounters",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
