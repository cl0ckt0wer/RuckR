using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuckR.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTimedRecruitmentSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecruitmentBaseDurationSeconds",
                table: "PlayerEncounters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecruitmentCompletesAtUtc",
                table: "PlayerEncounters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecruitmentItemKind",
                table: "PlayerEncounters",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<int>(
                name: "RecruitmentLocalPlayerCount",
                table: "PlayerEncounters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecruitmentRequiredDurationSeconds",
                table: "PlayerEncounters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecruitmentStartedAtUtc",
                table: "PlayerEncounters",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecruitmentBaseDurationSeconds",
                table: "PlayerEncounters");

            migrationBuilder.DropColumn(
                name: "RecruitmentCompletesAtUtc",
                table: "PlayerEncounters");

            migrationBuilder.DropColumn(
                name: "RecruitmentItemKind",
                table: "PlayerEncounters");

            migrationBuilder.DropColumn(
                name: "RecruitmentLocalPlayerCount",
                table: "PlayerEncounters");

            migrationBuilder.DropColumn(
                name: "RecruitmentRequiredDurationSeconds",
                table: "PlayerEncounters");

            migrationBuilder.DropColumn(
                name: "RecruitmentStartedAtUtc",
                table: "PlayerEncounters");
        }
    }
}
