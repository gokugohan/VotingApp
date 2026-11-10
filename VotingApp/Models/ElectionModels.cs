using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using VotingApp.Application.Elections;
using VotingApp.Domain.Elections;
using VotingApp.Domain.Elections.Rules;
using VotingApp.Infrastructure.Identity;

namespace VotingApp.Models
{
    public class CreateElectionModel
    {
        [Required, StringLength(200)]
        public string Name { get; set; }

        public ElectionType Type { get; set; } = ElectionType.Presidential;

        [DataType(DataType.Date), Display(Name = "Election date")]
        public DateTime ElectionDate { get; set; } = DateTime.Today;

        [Range(1, 500), Display(Name = "Seats (parliamentary)")]
        public int? TotalSeats { get; set; } = ParliamentarySeatAllocator.DefaultSeats;

        [Range(0, 99.99), Display(Name = "Threshold % (parliamentary)")]
        public decimal? ThresholdPercent { get; set; } = ParliamentarySeatAllocator.DefaultThresholdPercent;
    }

    public class AddContestantModel
    {
        public Guid ElectionId { get; set; }

        [StringLength(200)]
        public string Name { get; set; }

        [StringLength(30)]
        public string Acronym { get; set; }

        public ContestantType Type { get; set; }

        [Display(Name = "Linked candidate profile")]
        public Guid? CandidateId { get; set; }

        /// <summary>Parliamentary: an existing party record (otherwise one is found or created by name).</summary>
        public Guid? PartyId { get; set; }
    }

    public class ContestantVoteField
    {
        public Guid ContestantId { get; set; }
        public string Name { get; set; }

        [Range(0, int.MaxValue)]
        public int? Votes { get; set; }
    }

    /// <summary>Paper results sheet form, used for both first entry and blind verification.</summary>
    public class SheetFormModel
    {
        public Guid RoundId { get; set; }
        public Guid StationId { get; set; }
        public Guid? SheetId { get; set; }

        [Range(0, int.MaxValue), Display(Name = "Registered voters")] public int? RegisteredVoters { get; set; }
        [Required, Range(0, int.MaxValue), Display(Name = "Ballots received")] public int? BallotsReceived { get; set; }
        [Required, Range(0, int.MaxValue), Display(Name = "Ballots used (in the box)")] public int? BallotsUsed { get; set; }
        [Required, Range(0, int.MaxValue), Display(Name = "Spoiled ballots")] public int? BallotsSpoiled { get; set; }
        [Required, Range(0, int.MaxValue), Display(Name = "Unused ballots")] public int? BallotsUnused { get; set; }
        [Required, Range(0, int.MaxValue), Display(Name = "Blank votes")] public int? BlankVotes { get; set; }
        [Required, Range(0, int.MaxValue), Display(Name = "Invalid votes")] public int? InvalidVotes { get; set; }

        public List<ContestantVoteField> Votes { get; set; } = new();

        /// <summary>Optional photo/scan of the signed paper sheet (JPEG, PNG or PDF).</summary>
        [Display(Name = "Photo or scan of the signed sheet")]
        public IFormFile Scan { get; set; }

        public SheetFiguresInput ToInput() => new(
            RegisteredVoters, BallotsReceived, BallotsUsed, BallotsSpoiled, BallotsUnused,
            BlankVotes ?? 0, InvalidVotes ?? 0,
            Votes.ToDictionary(v => v.ContestantId, v => v.Votes ?? 0));

        public static SheetFormModel Blank(Guid roundId, Guid stationId, IEnumerable<Contestant> ballot) => new()
        {
            RoundId = roundId,
            StationId = stationId,
            Votes = ballot.Select(c => new ContestantVoteField { ContestantId = c.Id, Name = c.Name }).ToList(),
        };

        public static SheetFormModel From(ResultSheet sheet, IEnumerable<Contestant> ballot) => new()
        {
            RoundId = sheet.ElectionRoundId,
            StationId = sheet.PollingStationAreaId,
            SheetId = sheet.Id,
            RegisteredVoters = sheet.RegisteredVoters,
            BallotsReceived = sheet.BallotsReceived,
            BallotsUsed = sheet.BallotsUsed,
            BallotsSpoiled = sheet.BallotsSpoiled,
            BallotsUnused = sheet.BallotsUnused,
            BlankVotes = sheet.BlankVotes,
            InvalidVotes = sheet.InvalidVotes,
            Votes = ballot.Select(c => new ContestantVoteField
            {
                ContestantId = c.Id,
                Name = c.Name,
                Votes = sheet.Entries.FirstOrDefault(e => e.ContestantId == c.Id)?.Votes,
            }).ToList(),
        };
    }

    public class SheetPageModel
    {
        public EntryForm Context { get; set; }
        public SheetFormModel Form { get; set; }
        /// <summary>Verification mode hides the first entry's figures.</summary>
        public bool IsVerification { get; set; }
    }

    public class UserScopeRow
    {
        public AppUser User { get; set; }
        public IReadOnlyCollection<string> MunicipalityIds { get; set; }
    }

    public class RoundResultsPage
    {
        public RoundSummary Summary { get; set; }
        public RoundBreakdown Breakdown { get; set; }
        /// <summary>Live-refresh fingerprint (see PublicResultsService.GetSignatureAsync).</summary>
        public string Signature { get; set; }
        /// <summary>Map mode: null = leading contestant per municipality; otherwise that contestant's vote share.</summary>
        public Guid? MapContestantId { get; set; }
        /// <summary>Other rounds to switch to (home page only).</summary>
        public IReadOnlyList<PublishedRound> Rounds { get; set; }
        /// <summary>URL of the page itself without query string (for the map mode form).</summary>
        public string PagePath { get; set; }
        /// <summary>Parliamentary only: who fills the projected seats.</summary>
        public IReadOnlyList<PartyElected> Elected { get; set; }
        /// <summary>Same round of the previous election of this type, if any.</summary>
        public ResultsComparison Comparison { get; set; }
    }

    public class PartyListForm
    {
        public Guid ContestantId { get; set; }
        /// <summary>One person per line: "Full name, F" or "Full name, M".</summary>
        public string Effective { get; set; }
        public string Substitutes { get; set; }
    }

    public class PartyListPage
    {
        public PartyList List { get; set; }
        public PartyListForm Form { get; set; }
    }

    public class MunicipalityResultsPage
    {
        public RoundSummary Summary { get; set; }
        public IReadOnlyList<BallotContestant> Ballot { get; set; }
        public MunicipalityResult Municipality { get; set; }
        public IReadOnlyList<StationResult> Stations { get; set; }
    }
}
