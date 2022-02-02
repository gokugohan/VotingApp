using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VotingApp.Migrations
{
    public partial class AddPollingStationAndChangeTableName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoteCandidates");

            migrationBuilder.DropTable(
                name: "VotingAreas");

            migrationBuilder.CreateTable(
                name: "PollingStations",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollingStations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PollingStationArea",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    PollingStationId = table.Column<Guid>(nullable: false),
                    AreaId = table.Column<string>(nullable: false),
                    Description = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollingStationArea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollingStationArea_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PollingStationArea_PollingStations_PollingStationId",
                        column: x => x.PollingStationId,
                        principalTable: "PollingStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    CandidateId = table.Column<Guid>(nullable: false),
                    PollingStationAreaId = table.Column<Guid>(nullable: false),
                    Total = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Votes_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Votes_PollingStationArea_PollingStationAreaId",
                        column: x => x.PollingStationAreaId,
                        principalTable: "PollingStationArea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PollingStationArea_AreaId",
                table: "PollingStationArea",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_PollingStationArea_PollingStationId",
                table: "PollingStationArea",
                column: "PollingStationId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_CandidateId",
                table: "Votes",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollingStationAreaId",
                table: "Votes",
                column: "PollingStationAreaId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Votes");

            migrationBuilder.DropTable(
                name: "PollingStationArea");

            migrationBuilder.DropTable(
                name: "PollingStations");

            migrationBuilder.CreateTable(
                name: "VotingAreas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VotingAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VotingAreas_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VoteCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    VotingAreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoteCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoteCandidates_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VoteCandidates_VotingAreas_VotingAreaId",
                        column: x => x.VotingAreaId,
                        principalTable: "VotingAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoteCandidates_CandidateId",
                table: "VoteCandidates",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_VoteCandidates_VotingAreaId",
                table: "VoteCandidates",
                column: "VotingAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_VotingAreas_AreaId",
                table: "VotingAreas",
                column: "AreaId");
        }
    }
}
