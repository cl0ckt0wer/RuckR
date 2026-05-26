using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RuckR.Server.Data;

#nullable disable

namespace RuckR.Server.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(RuckRDbContext))]
    [Migration("20260526170000_PlacesBackedPitchGyms")]
    public partial class PlacesBackedPitchGyms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CreatorUserId",
                table: "Pitches",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.spatial_indexes si
                    INNER JOIN sys.index_columns ic
                        ON si.object_id = ic.object_id
                       AND si.index_id = ic.index_id
                    INNER JOIN sys.columns c
                        ON ic.object_id = c.object_id
                       AND ic.column_id = c.column_id
                    WHERE si.object_id = OBJECT_ID(N'[dbo].[Pitches]')
                      AND c.name = N'Location'
                )
                BEGIN
                    CREATE SPATIAL INDEX [IX_Pitches_Location_Spatial]
                    ON [dbo].[Pitches]([Location])
                    USING GEOGRAPHY_AUTO_GRID;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Pitches_Location_Spatial'
                      AND object_id = OBJECT_ID(N'[dbo].[Pitches]')
                )
                BEGIN
                    DROP INDEX [IX_Pitches_Location_Spatial] ON [dbo].[Pitches];
                END
                """);

            migrationBuilder.Sql(
                "UPDATE [Pitches] SET [CreatorUserId] = N'system' WHERE [CreatorUserId] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "CreatorUserId",
                table: "Pitches",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
