using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VotingApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PartyId",
                table: "Contestants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Acronym = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contestants_PartyId",
                table: "Contestants",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_Name",
                table: "Parties",
                column: "Name",
                unique: true);

            // One registry record per party/coalition name already on a ballot (case and surrounding spaces
            // ignored), then link every party contestant to it so earlier results can be compared.
            migrationBuilder.Sql(@"
INSERT INTO [Parties] ([Id], [Name], [Acronym], [Kind], [Notes], [CreatedAt])
SELECT NEWID(), x.[Name], x.[Acronym], x.[Kind], NULL, SYSUTCDATETIME()
FROM (
    SELECT LTRIM(RTRIM(c.[Name])) AS [Name], MAX(c.[Acronym]) AS [Acronym],
           MIN(CASE c.[Type] WHEN 3 THEN 2 ELSE 1 END) AS [Kind],
           ROW_NUMBER() OVER (PARTITION BY UPPER(LTRIM(RTRIM(c.[Name]))) ORDER BY MIN(e.[ElectionDate])) AS rn
    FROM [Contestants] c
    JOIN [Elections] e ON e.[Id] = c.[ElectionId]
    WHERE c.[Type] IN (2, 3)
    GROUP BY LTRIM(RTRIM(c.[Name]))
) x
WHERE x.rn = 1;

UPDATE c SET [PartyId] = p.[Id]
FROM [Contestants] c
JOIN [Parties] p ON UPPER(p.[Name]) = UPPER(LTRIM(RTRIM(c.[Name])))
WHERE c.[Type] IN (2, 3);
");

            migrationBuilder.AddForeignKey(
                name: "FK_Contestants_Parties_PartyId",
                table: "Contestants",
                column: "PartyId",
                principalTable: "Parties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contestants_Parties_PartyId",
                table: "Contestants");

            migrationBuilder.DropTable(
                name: "Parties");

            migrationBuilder.DropIndex(
                name: "IX_Contestants_PartyId",
                table: "Contestants");

            migrationBuilder.DropColumn(
                name: "PartyId",
                table: "Contestants");
        }
    }
}
