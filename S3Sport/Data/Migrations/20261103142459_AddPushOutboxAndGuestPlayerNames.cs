using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S3Sport.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPushOutboxAndGuestPlayerNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PushPending",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlayerNameText",
                table: "MatchEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedPlayerNameText",
                table: "MatchEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_PushPending",
                table: "Notifications",
                column: "PushPending",
                filter: "[PushPending] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_PushPending",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "PushPending",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "PlayerNameText",
                table: "MatchEvents");

            migrationBuilder.DropColumn(
                name: "RelatedPlayerNameText",
                table: "MatchEvents");
        }
    }
}
