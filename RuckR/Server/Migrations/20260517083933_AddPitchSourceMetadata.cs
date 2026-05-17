using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuckR.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPitchSourceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalPlaceId",
                table: "Pitches",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Pitches",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<string>(
                name: "SourceCategory",
                table: "Pitches",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceConfidence",
                table: "Pitches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceMatchReason",
                table: "Pitches",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pitches_ExternalPlaceId",
                table: "Pitches",
                column: "ExternalPlaceId",
                unique: true,
                filter: "[ExternalPlaceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pitches_ExternalPlaceId",
                table: "Pitches");

            migrationBuilder.DropColumn(
                name: "ExternalPlaceId",
                table: "Pitches");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Pitches");

            migrationBuilder.DropColumn(
                name: "SourceCategory",
                table: "Pitches");

            migrationBuilder.DropColumn(
                name: "SourceConfidence",
                table: "Pitches");

            migrationBuilder.DropColumn(
                name: "SourceMatchReason",
                table: "Pitches");
        }
    }
}
