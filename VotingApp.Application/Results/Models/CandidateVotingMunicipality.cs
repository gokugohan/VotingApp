using VotingApp.Domain.Entities;
using System;
using System.Collections.Generic;

namespace VotingApp.Application.Results
{
    public class CandidateVotingMunicipality
    {
        public Guid CandidateId { get; set; }
        public String CandidateName { get; set; }
        public VotingByMunicipality VotingByMunicipality { get; set; }

    }
}
