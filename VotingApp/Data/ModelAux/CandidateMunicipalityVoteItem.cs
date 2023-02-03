using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Data.ModelAux
{
    public class CandidateMunicipalityVoteItem
    {
        public Candidate Candidate { get; set; }
        public Area Area { get; set; }
        public string AreaKey { get; set; }
        public int Total { get; set; }
    }
}
