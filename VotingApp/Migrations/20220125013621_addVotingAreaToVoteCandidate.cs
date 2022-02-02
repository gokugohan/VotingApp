using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VotingApp.Migrations
{
    public partial class addVotingAreaToVoteCandidate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VotingAreaId",
                table: "VoteCandidates",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Avatar",
                table: "Candidates",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoteCandidates_VotingAreaId",
                table: "VoteCandidates",
                column: "VotingAreaId");

            migrationBuilder.AddForeignKey(
                name: "FK_VoteCandidates_VotingAreas_VotingAreaId",
                table: "VoteCandidates",
                column: "VotingAreaId",
                principalTable: "VotingAreas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VoteCandidates_VotingAreas_VotingAreaId",
                table: "VoteCandidates");

            migrationBuilder.DropIndex(
                name: "IX_VoteCandidates_VotingAreaId",
                table: "VoteCandidates");

            migrationBuilder.DropColumn(
                name: "VotingAreaId",
                table: "VoteCandidates");

            migrationBuilder.DropColumn(
                name: "Avatar",
                table: "Candidates");
        }
    }
}
