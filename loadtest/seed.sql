/*
  Results-night load test data. FOR TEST DATABASES ONLY: refuses to run if any election exists.

  Volume modelled on a Timor-Leste presidential election:
    13 municipalities, 65 administrative posts, 390 villages, 1,560 polling stations
    1 presidential election, round 1 in Counting, 16 candidates
    1,000 stations already approved (16,000 vote rows); 560 left for clerks to enter
    50 clerk accounts (loadclerk01..50@example.test, password LoadTest123) with
    enter + verify permissions, assigned to every municipality.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON; -- required by filtered indexes (sqlcmd defaults to OFF)

IF EXISTS (SELECT 1 FROM Elections)
    THROW 50000, 'seed.sql is for an empty test database only (elections already exist).', 1;

BEGIN TRANSACTION;

DECLARE @ElectionId uniqueidentifier = '7e57e1ec-0000-0000-0000-000000000001';
DECLARE @RoundId    uniqueidentifier = '7e57e1ec-0000-0000-0000-000000000002';

-- ---------- Areas and polling stations ----------
DECLARE @m int = 1, @p int, @v int, @s int;
DECLARE @mid nvarchar(450), @pid nvarchar(450), @vid nvarchar(450);
WHILE @m <= 13
BEGIN
    SET @mid = CONCAT('TLS', RIGHT(CONCAT('000', @m), 3));
    INSERT INTO Areas (Id, Name, ParentId, Level)
    VALUES (@mid, CHOOSE(@m, 'Aileu', 'Ainaro', 'Baucau', 'Bobonaro', 'Covalima', 'Dili', 'Ermera',
                             'Lautem', 'Liquica', 'Manatuto', 'Manufahi', 'Oecusse', 'Viqueque'), NULL, 2);
    SET @p = 1;
    WHILE @p <= 5
    BEGIN
        SET @pid = CONCAT(@mid, RIGHT(CONCAT('00', @p), 2));
        INSERT INTO Areas (Id, Name, ParentId, Level) VALUES (@pid, CONCAT('Post ', @m, '-', @p), @mid, 3);
        SET @v = 1;
        WHILE @v <= 6
        BEGIN
            SET @vid = CONCAT(@pid, RIGHT(CONCAT('00', @v), 2));
            INSERT INTO Areas (Id, Name, ParentId, Level) VALUES (@vid, CONCAT('Suco ', @m, '-', @p, '-', @v), @pid, 4);
            SET @s = 1;
            WHILE @s <= 4
            BEGIN
                DECLARE @psId uniqueidentifier = NEWID();
                INSERT INTO PollingStations (Id, Name) VALUES (@psId, CONCAT('Station ', @vid, '-', @s));
                INSERT INTO PollingStationArea (Id, PollingStationId, AreaId) VALUES (NEWID(), @psId, @vid);
                SET @s += 1;
            END
            SET @v += 1;
        END
        SET @p += 1;
    END
    SET @m += 1;
END

-- ---------- Election, round, 16 candidates ----------
INSERT INTO Elections (Id, Name, Type, Status, ElectionDate, TotalSeats, ThresholdPercent, Notes, CreatedAt)
VALUES (@ElectionId, N'Load test presidential', 1, 2, '2027-03-20', NULL, NULL, N'Load test data', SYSUTCDATETIME());
INSERT INTO ElectionRounds (Id, ElectionId, Number, VotingDate, Status) VALUES (@RoundId, @ElectionId, 1, '2027-03-20', 2);

DECLARE @c int = 1;
WHILE @c <= 16
BEGIN
    DECLARE @cid uniqueidentifier = NEWID();
    INSERT INTO Contestants (Id, ElectionId, Type, Name, Acronym)
    VALUES (@cid, @ElectionId, 1, CONCAT('Candidate ', RIGHT(CONCAT('0', @c), 2)), NULL);
    INSERT INTO RoundContestants (ElectionRoundId, ContestantId, BallotPosition) VALUES (@RoundId, @cid, @c);
    SET @c += 1;
END

-- ---------- 1,000 approved sheets with consistent figures ----------
SELECT TOP (1000) Id INTO #counted FROM PollingStationArea ORDER BY AreaId, Id;

INSERT INTO ResultSheets (Id, ElectionRoundId, PollingStationAreaId, BlankVotes, InvalidVotes, Status,
                          EnteredByUserId, EnteredAt, VerifiedByUserId, VerifiedAt, ApprovedByUserId, ApprovedAt, VerificationMismatches)
SELECT NEWID(), @RoundId, Id, ABS(CHECKSUM(NEWID())) % 8, ABS(CHECKSUM(NEWID())) % 6, 3,
       'seed', SYSUTCDATETIME(), 'seed', SYSUTCDATETIME(), 'seed', SYSUTCDATETIME(), 0
FROM #counted;

INSERT INTO ResultSheetEntries (Id, ResultSheetId, ContestantId, Votes)
SELECT NEWID(), s.Id, rc.ContestantId,
       -- Skewed so a few candidates lead: votes fall with ballot position.
       ABS(CHECKSUM(NEWID())) % (120 / rc.BallotPosition + 5)
FROM ResultSheets s CROSS JOIN RoundContestants rc
WHERE s.ElectionRoundId = @RoundId AND rc.ElectionRoundId = @RoundId;

UPDATE s SET
    BallotsUsed = t.Valid + s.BlankVotes + s.InvalidVotes,
    BallotsSpoiled = 2,
    BallotsUnused = 50,
    BallotsReceived = t.Valid + s.BlankVotes + s.InvalidVotes + 52,
    RegisteredVoters = t.Valid + s.BlankVotes + s.InvalidVotes + 120
FROM ResultSheets s
JOIN (SELECT ResultSheetId, SUM(Votes) AS Valid FROM ResultSheetEntries GROUP BY ResultSheetId) t ON t.ResultSheetId = s.Id;

-- ---------- Clerk role and 50 clerks ----------
DECLARE @RoleId nvarchar(450) = '7e57e1ec-role-0000-0000-000000000001';
INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) VALUES (@RoleId, 'Load clerk', 'LOAD CLERK', NEWID());
INSERT INTO AspNetRoleClaims (RoleId, ClaimType, ClaimValue) VALUES
    (@RoleId, 'upstream.permission', 'PERMISSION.RESULTS.ENTER'),
    (@RoleId, 'upstream.permission', 'PERMISSION.RESULTS.VERIFY');

DECLARE @u int = 1;
WHILE @u <= 50
BEGIN
    DECLARE @uid nvarchar(450) = CONVERT(nvarchar(450), NEWID());
    DECLARE @email nvarchar(256) = CONCAT('loadclerk', RIGHT(CONCAT('0', @u), 2), '@example.test');
    INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash,
                             SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled,
                             AccessFailedCount, FirstName, LastName)
    VALUES (@uid, @email, UPPER(@email), @email, UPPER(@email), 1,
            -- Identity v3 hash of "LoadTest123" (PBKDF2-HMAC-SHA512, 100,000 iterations). Test only.
            'AQAAAAIAAYagAAAAEAABAgMEBQYHCAkKCwwNDg/+LkRIlYkWZxo8hvlWGfLePWwoWSaPVIrX+kFhe3C5zg==',
            CONVERT(nvarchar(36), NEWID()), CONVERT(nvarchar(36), NEWID()), 0, 0, 1, 0, 'Load', CONCAT('Clerk ', @u));
    INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@uid, @RoleId);
    INSERT INTO UserAreaAssignments (Id, UserId, MunicipalityId)
    SELECT NEWID(), @uid, Id FROM Areas WHERE Level = 2;
    SET @u += 1;
END

COMMIT;

SELECT
    (SELECT COUNT(*) FROM PollingStationArea) AS stations,
    (SELECT COUNT(*) FROM ResultSheets) AS approved_sheets,
    (SELECT COUNT(*) FROM ResultSheetEntries) AS entries,
    (SELECT COUNT(*) FROM AspNetUsers WHERE Email LIKE 'loadclerk%') AS clerks;
