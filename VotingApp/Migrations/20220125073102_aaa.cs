using Microsoft.EntityFrameworkCore.Migrations;

namespace VotingApp.Migrations
{
    public partial class aaa : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VotingAreas_Areas_AreaId",
                table: "VotingAreas");

            migrationBuilder.AlterColumn<string>(
                name: "AreaId",
                table: "VotingAreas",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_VotingAreas_Areas_AreaId",
                table: "VotingAreas",
                column: "AreaId",
                principalTable: "Areas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VotingAreas_Areas_AreaId",
                table: "VotingAreas");

            migrationBuilder.AlterColumn<string>(
                name: "AreaId",
                table: "VotingAreas",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string));

            migrationBuilder.AddForeignKey(
                name: "FK_VotingAreas_Areas_AreaId",
                table: "VotingAreas",
                column: "AreaId",
                principalTable: "Areas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
