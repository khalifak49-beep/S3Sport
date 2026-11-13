using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S3Sport.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLineupPitchOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PitchOrder",
                table: "MatchLineups",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PitchOrder",
                table: "MatchLineups");
        }
    }
}
