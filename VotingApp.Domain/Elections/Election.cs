using VotingApp.Domain.Entities;
using System;
using System.Collections.Generic;

namespace VotingApp.Domain.Elections
{
    /// <summary>One election event, e.g. "Presidential Election 2027" or "Parliamentary Election 2028".</summary>
    public class Election
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public ElectionType Type { get; set; }
        public ElectionStatus Status { get; set; }
        public DateTime ElectionDate { get; set; }

        /// <summary>Parliamentary only: seats to allocate (65 for the National Parliament).</summary>
        public int? TotalSeats { get; set; }

        /// <summary>Parliamentary only: minimum share of valid votes for a list to get seats (e.g. 4.00).</summary>
        public decimal? ThresholdPercent { get; set; }

        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<ElectionRound> Rounds { get; set; } = new List<ElectionRound>();
        public ICollection<Contestant> Contestants { get; set; } = new List<Contestant>();
    }

    /// <summary>A voting day. Parliamentary elections have one; presidential may have a second-round runoff.</summary>
    public class ElectionRound
    {
        public Guid Id { get; set; }
        public Guid ElectionId { get; set; }
        public Election Election { get; set; }

        /// <summary>1 for the first round, 2 for a runoff.</summary>
        public int Number { get; set; }
        public DateTime VotingDate { get; set; }
        public ElectionStatus Status { get; set; }

        /// <summary>Contestants on this round's ballot (a runoff only has the top two).</summary>
        public ICollection<RoundContestant> Contestants { get; set; } = new List<RoundContestant>();
        public ICollection<ResultSheet> ResultSheets { get; set; } = new List<ResultSheet>();
    }

    /// <summary>A presidential candidate, party list or coalition list standing in an election.</summary>
    public class Contestant
    {
        public Guid Id { get; set; }
        public Guid ElectionId { get; set; }
        public Election Election { get; set; }

        public ContestantType Type { get; set; }
        public string Name { get; set; }
        public string Acronym { get; set; }
        public string LogoPath { get; set; }
        public string Description { get; set; }

        /// <summary>Optional link to the legacy candidate record (profile, photo).</summary>
        public Guid? CandidateId { get; set; }
        public Candidate Candidate { get; set; }

        /// <summary>Parliamentary: the ordered closed list.</summary>
        public ICollection<PartyListCandidate> ListCandidates { get; set; } = new List<PartyListCandidate>();
    }

    public class RoundContestant
    {
        public Guid ElectionRoundId { get; set; }
        public ElectionRound ElectionRound { get; set; }
        public Guid ContestantId { get; set; }
        public Contestant Contestant { get; set; }

        /// <summary>Order on the ballot paper.</summary>
        public int BallotPosition { get; set; }
    }

    /// <summary>A person on a party/coalition's closed list.</summary>
    public class PartyListCandidate
    {
        public Guid Id { get; set; }
        public Guid ContestantId { get; set; }
        public Contestant Contestant { get; set; }

        public int Position { get; set; }
        public string FullName { get; set; }
        public Gender Gender { get; set; }
        public bool IsSubstitute { get; set; }
    }
}
