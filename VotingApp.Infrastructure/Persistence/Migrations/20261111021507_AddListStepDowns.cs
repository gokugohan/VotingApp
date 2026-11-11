using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VotingApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListStepDowns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StepDownNote",
                table: "PartyListCandidates",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StepDownReason",
                table: "PartyListCandidates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SteppedDownOn",
                table: "PartyListCandidates",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StepDownNote",
                table: "PartyListCandidates");

            migrationBuilder.DropColumn(
                name: "StepDownReason",
                table: "PartyListCandidates");

            migrationBuilder.DropColumn(
                name: "SteppedDownOn",
                table: "PartyListCandidates");
        }
    }
}
