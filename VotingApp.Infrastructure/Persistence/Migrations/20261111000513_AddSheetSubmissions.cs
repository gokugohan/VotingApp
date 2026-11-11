using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VotingApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSheetSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SheetSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ElectionRoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PollingStationAreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QueuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SheetSubmissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SheetSubmissions_ElectionRoundId_PollingStationAreaId",
                table: "SheetSubmissions",
                columns: new[] { "ElectionRoundId", "PollingStationAreaId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SheetSubmissions");
        }
    }
}
