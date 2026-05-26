using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuckR.Server.Migrations
{
    /// <inheritdoc />
    public partial class UserFirstRpslsBattles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Battles_ChallengerId",
                table: "Battles");

            migrationBuilder.DropIndex(
                name: "IX_Battles_OpponentId",
                table: "Battles");

            migrationBuilder.AlterColumn<int>(
                name: "OpponentPlayerId",
                table: "Battles",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "ChallengerPlayerId",
                table: "Battles",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "Battles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChallengerMove",
                table: "Battles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ChallengerScore",
                table: "Battles",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChallengerSubmittedAt",
                table: "Battles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpponentMove",
                table: "Battles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OpponentScore",
                table: "Battles",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpponentSubmittedAt",
                table: "Battles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionMethod",
                table: "Battles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Battles_ChallengerId_Status_CreatedAt",
                table: "Battles",
                columns: new[] { "ChallengerId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Battles_OpponentId_Status_CreatedAt",
                table: "Battles",
                columns: new[] { "OpponentId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Battles_ChallengerId_Status_CreatedAt",
                table: "Battles");

            migrationBuilder.DropIndex(
                name: "IX_Battles_OpponentId_Status_CreatedAt",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "ChallengerMove",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "ChallengerScore",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "ChallengerSubmittedAt",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "OpponentMove",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "OpponentScore",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "OpponentSubmittedAt",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "ResolutionMethod",
                table: "Battles");

            migrationBuilder.AlterColumn<int>(
                name: "OpponentPlayerId",
                table: "Battles",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ChallengerPlayerId",
                table: "Battles",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Battles_ChallengerId",
                table: "Battles",
                column: "ChallengerId");

            migrationBuilder.CreateIndex(
                name: "IX_Battles_OpponentId",
                table: "Battles",
                column: "OpponentId");
        }
    }
}
