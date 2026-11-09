using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VotingApp.Migrations
{
    /// <inheritdoc />
    public partial class AddElectionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Votes_CandidateId",
                table: "Votes");

            migrationBuilder.CreateTable(
                name: "Elections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ElectionDate = table.Column<DateTime>(type: "date", nullable: false),
                    TotalSeats = table.Column<int>(type: "int", nullable: true),
                    ThresholdPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Elections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contestants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ElectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Acronym = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contestants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contestants_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contestants_Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ElectionRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ElectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    VotingDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectionRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectionRounds_Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyListCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContestantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    IsSubstitute = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyListCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyListCandidates_Contestants_ContestantId",
                        column: x => x.ContestantId,
                        principalTable: "Contestants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResultSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ElectionRoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PollingStationAreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisteredVoters = table.Column<int>(type: "int", nullable: true),
                    BallotsReceived = table.Column<int>(type: "int", nullable: true),
                    BallotsUsed = table.Column<int>(type: "int", nullable: true),
                    BallotsSpoiled = table.Column<int>(type: "int", nullable: true),
                    BallotsUnused = table.Column<int>(type: "int", nullable: true),
                    BlankVotes = table.Column<int>(type: "int", nullable: false),
                    InvalidVotes = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScanPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EnteredByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EnteredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultSheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultSheets_ElectionRounds_ElectionRoundId",
                        column: x => x.ElectionRoundId,
                        principalTable: "ElectionRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResultSheets_PollingStationArea_PollingStationAreaId",
                        column: x => x.PollingStationAreaId,
                        principalTable: "PollingStationArea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoundContestants",
                columns: table => new
                {
                    ElectionRoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContestantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BallotPosition = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundContestants", x => new { x.ElectionRoundId, x.ContestantId });
                    table.ForeignKey(
                        name: "FK_RoundContestants_Contestants_ContestantId",
                        column: x => x.ContestantId,
                        principalTable: "Contestants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoundContestants_ElectionRounds_ElectionRoundId",
                        column: x => x.ElectionRoundId,
                        principalTable: "ElectionRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResultSheetEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultSheetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContestantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Votes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultSheetEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultSheetEntries_Contestants_ContestantId",
                        column: x => x.ContestantId,
                        principalTable: "Contestants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultSheetEntries_ResultSheets_ResultSheetId",
                        column: x => x.ResultSheetId,
                        principalTable: "ResultSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Copy existing votes into an "imported" election so the new model starts with the current data.
            // The legacy Votes table is left untouched: the current dashboard keeps reading it.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [Votes])
BEGIN
    DECLARE @ElectionId uniqueidentifier = NEWID();
    DECLARE @RoundId uniqueidentifier = NEWID();
    DECLARE @Today date = CAST(SYSUTCDATETIME() AS date);

    -- Parliamentary because the existing candidates are party lists; change Type if that is wrong.
    INSERT INTO [Elections] ([Id], [Name], [Type], [Status], [ElectionDate], [TotalSeats], [ThresholdPercent], [Notes], [CreatedAt])
    VALUES (@ElectionId, N'Imported results', 2, 3, @Today, 65, 4.00,
            N'Created by migration AddElectionModel from the legacy Votes table. Ballot reconciliation figures were not recorded.',
            SYSUTCDATETIME());

    INSERT INTO [ElectionRounds] ([Id], [ElectionId], [Number], [VotingDate], [Status])
    VALUES (@RoundId, @ElectionId, 1, @Today, 3);

    -- Contestant names must be unique per election; disambiguate duplicate candidate names.
    INSERT INTO [Contestants] ([Id], [ElectionId], [Type], [Name], [LogoPath], [Description], [CandidateId])
    SELECT NEWID(), @ElectionId, 2,
           CASE WHEN COUNT(*) OVER (PARTITION BY c.[Name]) > 1
                THEN LEFT(ISNULL(c.[Name], N'Unnamed'), 185) + N' (' + LEFT(CAST(c.[Id] AS nvarchar(36)), 8) + N')'
                ELSE LEFT(ISNULL(c.[Name], N'Unnamed'), 200) END,
           LEFT(c.[Avatar], 300), LEFT(c.[Description], 2000), c.[Id]
    FROM [Candidates] c;

    INSERT INTO [RoundContestants] ([ElectionRoundId], [ContestantId], [BallotPosition])
    SELECT @RoundId, ct.[Id], ROW_NUMBER() OVER (ORDER BY ct.[Name])
    FROM [Contestants] ct WHERE ct.[ElectionId] = @ElectionId;

    INSERT INTO [ResultSheets] ([Id], [ElectionRoundId], [PollingStationAreaId], [BlankVotes], [InvalidVotes], [Status], [Notes])
    SELECT NEWID(), @RoundId, v.[PollingStationAreaId], 0, 0, 1, N'Imported from legacy Votes'
    FROM (SELECT DISTINCT [PollingStationAreaId] FROM [Votes]) v;

    INSERT INTO [ResultSheetEntries] ([Id], [ResultSheetId], [ContestantId], [Votes])
    SELECT NEWID(), rs.[Id], ct.[Id], SUM(v.[Total])
    FROM [Votes] v
    JOIN [ResultSheets] rs ON rs.[PollingStationAreaId] = v.[PollingStationAreaId] AND rs.[ElectionRoundId] = @RoundId
    JOIN [Contestants] ct ON ct.[CandidateId] = v.[CandidateId] AND ct.[ElectionId] = @ElectionId
    GROUP BY rs.[Id], ct.[Id];
END
");

            // Fail with a clear message (and roll back the whole migration) instead of an opaque
            // index error if the legacy table already holds duplicate candidate/station rows.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT [CandidateId], [PollingStationAreaId] FROM [Votes]
           GROUP BY [CandidateId], [PollingStationAreaId] HAVING COUNT(*) > 1)
    THROW 50001, N'Votes contains duplicate rows for the same candidate and polling station. Merge them (keep one row per pair) and run the migration again. Find them with: SELECT CandidateId, PollingStationAreaId, COUNT(*) FROM Votes GROUP BY CandidateId, PollingStationAreaId HAVING COUNT(*) > 1', 1;
");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_CandidateId_PollingStationAreaId",
                table: "Votes",
                columns: new[] { "CandidateId", "PollingStationAreaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contestants_CandidateId",
                table: "Contestants",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_Contestants_ElectionId_Name",
                table: "Contestants",
                columns: new[] { "ElectionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectionRounds_ElectionId_Number",
                table: "ElectionRounds",
                columns: new[] { "ElectionId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyListCandidates_ContestantId_Position_IsSubstitute",
                table: "PartyListCandidates",
                columns: new[] { "ContestantId", "Position", "IsSubstitute" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultSheetEntries_ContestantId",
                table: "ResultSheetEntries",
                column: "ContestantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultSheetEntries_ResultSheetId_ContestantId",
                table: "ResultSheetEntries",
                columns: new[] { "ResultSheetId", "ContestantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultSheets_ElectionRoundId_PollingStationAreaId",
                table: "ResultSheets",
                columns: new[] { "ElectionRoundId", "PollingStationAreaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultSheets_ElectionRoundId_Status",
                table: "ResultSheets",
                columns: new[] { "ElectionRoundId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultSheets_PollingStationAreaId",
                table: "ResultSheets",
                column: "PollingStationAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundContestants_ContestantId",
                table: "RoundContestants",
                column: "ContestantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartyListCandidates");

            migrationBuilder.DropTable(
                name: "ResultSheetEntries");

            migrationBuilder.DropTable(
                name: "RoundContestants");

            migrationBuilder.DropTable(
                name: "ResultSheets");

            migrationBuilder.DropTable(
                name: "Contestants");

            migrationBuilder.DropTable(
                name: "ElectionRounds");

            migrationBuilder.DropTable(
                name: "Elections");

            migrationBuilder.DropIndex(
                name: "IX_Votes_CandidateId_PollingStationAreaId",
                table: "Votes");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_CandidateId",
                table: "Votes",
                column: "CandidateId");
        }
    }
}
