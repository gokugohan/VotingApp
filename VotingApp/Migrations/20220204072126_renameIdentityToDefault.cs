using Microsoft.EntityFrameworkCore.Migrations;

namespace VotingApp.Migrations
{
    public partial class renameIdentityToDefault : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Votes",
                schema: "Identity",
                newName: "Votes");

            migrationBuilder.RenameTable(
                name: "UserTokens",
                schema: "Identity",
                newName: "UserTokens");

            migrationBuilder.RenameTable(
                name: "UserRoles",
                schema: "Identity",
                newName: "UserRoles");

            migrationBuilder.RenameTable(
                name: "UserLogins",
                schema: "Identity",
                newName: "UserLogins");

            migrationBuilder.RenameTable(
                name: "UserClaims",
                schema: "Identity",
                newName: "UserClaims");

            migrationBuilder.RenameTable(
                name: "User",
                schema: "Identity",
                newName: "User");

            migrationBuilder.RenameTable(
                name: "RoleClaims",
                schema: "Identity",
                newName: "RoleClaims");

            migrationBuilder.RenameTable(
                name: "Role",
                schema: "Identity",
                newName: "Role");

            migrationBuilder.RenameTable(
                name: "PollingStations",
                schema: "Identity",
                newName: "PollingStations");

            migrationBuilder.RenameTable(
                name: "PollingStationArea",
                schema: "Identity",
                newName: "PollingStationArea");

            migrationBuilder.RenameTable(
                name: "Candidates",
                schema: "Identity",
                newName: "Candidates");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                schema: "Identity",
                newName: "AuditLogs");

            migrationBuilder.RenameTable(
                name: "Areas",
                schema: "Identity",
                newName: "Areas");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Identity");

            migrationBuilder.RenameTable(
                name: "Votes",
                newName: "Votes",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "UserTokens",
                newName: "UserTokens",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "UserRoles",
                newName: "UserRoles",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "UserLogins",
                newName: "UserLogins",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "UserClaims",
                newName: "UserClaims",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "User",
                newName: "User",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "RoleClaims",
                newName: "RoleClaims",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "Role",
                newName: "Role",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "PollingStations",
                newName: "PollingStations",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "PollingStationArea",
                newName: "PollingStationArea",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "Candidates",
                newName: "Candidates",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                newName: "AuditLogs",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "Areas",
                newName: "Areas",
                newSchema: "Identity");
        }
    }
}
