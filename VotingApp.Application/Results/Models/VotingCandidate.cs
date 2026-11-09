using VotingApp.Domain.Entities;
using System;
using System.Collections.Generic;

namespace VotingApp.Application.Results
{
    public class VotingCandidate
    {
        public Guid CandidateId { get; set; }
        public string CandidateName { get; set; }
        public string MunicipalityName { get; set; }
        public int TotalVote { get; set; }
    }
}
