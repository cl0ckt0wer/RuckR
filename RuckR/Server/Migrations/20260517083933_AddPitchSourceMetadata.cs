using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuckR.Server.Migrations
{
    /// <summary>Defines the server-side class AddPitchSourceMetadata.</summary>
    public partial class AddPitchSourceMetadata : Migration
    {
        /// <summary>Add source metadata columns to pitches.</summary>
        /// <param name="migrationBuilder">The migration builder.</param>
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
        /// <summary>Remove pitch source metadata columns.</summary>
        /// <param name="migrationBuilder">The migration builder.</param>
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

