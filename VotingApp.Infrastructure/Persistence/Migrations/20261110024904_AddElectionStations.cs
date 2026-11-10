using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VotingApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddElectionStations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElectionStations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ElectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PollingStationAreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AreaId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    VillageName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdministrativePostId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AdministrativePostName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MunicipalityId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MunicipalityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FrozenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectionStations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectionStations_Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElectionStations_ElectionId_PollingStationAreaId",
                table: "ElectionStations",
                columns: new[] { "ElectionId", "PollingStationAreaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectionStations_PollingStationAreaId",
                table: "ElectionStations",
                column: "PollingStationAreaId");

            // Elections that are already open keep the station list they have today; later station
            // changes then no longer affect them. Elections still in set-up use the live list until they open.
            migrationBuilder.Sql(@"
INSERT INTO [ElectionStations] ([Id], [ElectionId], [PollingStationAreaId], [StationName], [AreaId], [VillageName],
    [AdministrativePostId], [AdministrativePostName], [MunicipalityId], [MunicipalityName], [FrozenAtUtc])
SELECT NEWID(), e.[Id], psa.[Id], LEFT(ISNULL(ps.[Name], N'(unnamed)'), 200), psa.[AreaId], LEFT(v.[Name], 200),
       v.[ParentId], LEFT(post.[Name], 200), LEFT(psa.[AreaId], 6), LEFT(ISNULL(m.[Name], LEFT(psa.[AreaId], 6)), 200), SYSUTCDATETIME()
FROM [Elections] e
CROSS JOIN [PollingStationArea] psa
JOIN [PollingStations] ps ON ps.[Id] = psa.[PollingStationId]
LEFT JOIN [Areas] v ON v.[Id] = psa.[AreaId]
LEFT JOIN [Areas] post ON post.[Id] = v.[ParentId]
LEFT JOIN [Areas] m ON m.[Id] = LEFT(psa.[AreaId], 6)
WHERE e.[Status] >= 1 AND LEN(psa.[AreaId]) >= 6;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElectionStations");
        }
    }
}
