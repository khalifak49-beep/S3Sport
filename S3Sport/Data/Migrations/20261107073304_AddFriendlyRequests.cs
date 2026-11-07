using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S3Sport.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendlyRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FriendlyRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    PreferredDate = table.Column<DateTime>(type: "date", nullable: false),
                    PreferredTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    VenuePreference = table.Column<int>(type: "int", nullable: false),
                    VenueId = table.Column<int>(type: "int", nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendlyRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FriendlyRequests_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FriendlyRequests_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FriendlyRequests_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FriendlyRequests_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FriendlyOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FriendlyRequestId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendlyOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FriendlyOffers_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FriendlyOffers_FriendlyRequests_FriendlyRequestId",
                        column: x => x.FriendlyRequestId,
                        principalTable: "FriendlyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FriendlyOffers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FriendlyOffers_CreatedByUserId",
                table: "FriendlyOffers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendlyOffers_FriendlyRequestId_TeamId",
                table: "FriendlyOffers",
                columns: new[] { "FriendlyRequestId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FriendlyOffers_TeamId",
                table: "FriendlyOffers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendlyRequests_CreatedByUserId",
                table: "FriendlyRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendlyRequests_MatchId",
                table: "FriendlyRequests",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendlyRequests_Status_PreferredDate",
                table: "FriendlyRequests",
                columns: new[] { "Status", "PreferredDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FriendlyRequests_TeamId",
                table: "FriendlyRequests",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendlyRequests_VenueId",
                table: "FriendlyRequests",
                column: "VenueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FriendlyOffers");

            migrationBuilder.DropTable(
                name: "FriendlyRequests");
        }
    }
}
