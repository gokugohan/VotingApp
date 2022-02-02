using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Data.ModelAux
{
    public class CandidateVotingMunicipality
    {
        public Guid CandidateId { get; set; }
        public String CandidateName { get; set; }
        public VotingByMunicipality VotingByMunicipality { get; set; }

    }
}
