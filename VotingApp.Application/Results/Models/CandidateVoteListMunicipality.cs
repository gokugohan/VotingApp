using VotingApp.Domain.Entities;
using System;
using System.Collections.Generic;

namespace VotingApp.Application.Results
{
    public class CandidateVoteListMunicipality
    {
        public Candidate Candidate { get; set; }
        public IList<int> Votes { get; set; }
    }
}
