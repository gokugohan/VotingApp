using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Data.ModelAux
{
    public class CandidateVoteListMunicipality
    {
        public Candidate Candidate { get; set; }
        public IList<int> Votes { get; set; }
    }
}
