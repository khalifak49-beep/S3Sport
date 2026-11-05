using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S3Sport.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendlyOrganizer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganizerTeamId",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matches_OrganizerTeamId",
                table: "Matches",
                column: "OrganizerTeamId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matches_Organizer",
                table: "Matches",
                sql: "[OrganizerTeamId] IS NULL OR ([CompetitionSeasonId] IS NULL AND [OrganizerTeamId] IN ([HomeTeamId], [AwayTeamId]))");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Teams_OrganizerTeamId",
                table: "Matches",
                column: "OrganizerTeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Teams_OrganizerTeamId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_OrganizerTeamId",
                table: "Matches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Matches_Organizer",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "OrganizerTeamId",
                table: "Matches");
        }
    }
}
