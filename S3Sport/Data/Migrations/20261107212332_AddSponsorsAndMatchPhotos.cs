using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S3Sport.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSponsorsAndMatchPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchPhotos_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchPhotos_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchPhotos_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sponsors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Tagline = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    GovernorateId = table.Column<int>(type: "int", nullable: true),
                    WilayatId = table.Column<int>(type: "int", nullable: true),
                    CompetitionId = table.Column<int>(type: "int", nullable: true),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Views = table.Column<int>(type: "int", nullable: false),
                    Clicks = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sponsors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sponsors_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sponsors_Governorates_GovernorateId",
                        column: x => x.GovernorateId,
                        principalTable: "Governorates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sponsors_Wilayats_WilayatId",
                        column: x => x.WilayatId,
                        principalTable: "Wilayats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchPhotos_MatchId_TeamId",
                table: "MatchPhotos",
                columns: new[] { "MatchId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchPhotos_TeamId",
                table: "MatchPhotos",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPhotos_UploadedByUserId",
                table: "MatchPhotos",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_CompetitionId",
                table: "Sponsors",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_GovernorateId",
                table: "Sponsors",
                column: "GovernorateId");

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_IsActive_Scope",
                table: "Sponsors",
                columns: new[] { "IsActive", "Scope" });

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_WilayatId",
                table: "Sponsors",
                column: "WilayatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchPhotos");

            migrationBuilder.DropTable(
                name: "Sponsors");
        }
    }
}
