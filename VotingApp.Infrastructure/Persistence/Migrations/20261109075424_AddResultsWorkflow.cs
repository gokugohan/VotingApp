using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VotingApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResultsWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastMismatchFields",
                table: "ResultSheets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAt",
                table: "ResultSheets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedByUserId",
                table: "ResultSheets",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerificationMismatches",
                table: "ResultSheets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "UserAreaAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MunicipalityId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAreaAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAreaAssignments_Areas_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAreaAssignments_MunicipalityId",
                table: "UserAreaAssignments",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAreaAssignments_UserId_MunicipalityId",
                table: "UserAreaAssignments",
                columns: new[] { "UserId", "MunicipalityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAreaAssignments");

            migrationBuilder.DropColumn(
                name: "LastMismatchFields",
                table: "ResultSheets");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                table: "ResultSheets");

            migrationBuilder.DropColumn(
                name: "LockedByUserId",
                table: "ResultSheets");

            migrationBuilder.DropColumn(
                name: "VerificationMismatches",
                table: "ResultSheets");
        }
    }
}
