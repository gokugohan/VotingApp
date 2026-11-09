using VotingApp.Domain.Entities;
using System;
using System.Collections.Generic;

namespace VotingApp.Application.Results
{
    public class CandidateMunicipalityVoteItem
    {
        public Candidate Candidate { get; set; }
        public Area Area { get; set; }
        public string AreaKey { get; set; }
        public int Total { get; set; }
    }
}
