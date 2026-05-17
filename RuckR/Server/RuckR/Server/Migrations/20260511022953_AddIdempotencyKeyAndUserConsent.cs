using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuckR.Server.RuckR.Server.Migrations
{
    /// <summary>Defines the server-side class AddIdempotencyKeyAndUserConsent.</summary>
    public partial class AddIdempotencyKeyAndUserConsent : Migration
    {
        /// <summary>Add idempotency support and consent tracking.</summary>
        /// <param name="migrationBuilder">The migration builder.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Battles",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConsentGivenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConsents", x => x.Id);
                });
        }
        /// <summary>Revert idempotency and consent tracking changes.</summary>
        /// <param name="migrationBuilder">The migration builder.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserConsents");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Battles");
        }
    }
}
