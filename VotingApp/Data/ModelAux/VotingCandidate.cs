using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Data.ModelAux
{
    public class VotingCandidate
    {
        public Guid CandidateId { get; set; }
        public string CandidateName { get; set; }
        public string MunicipalityName { get; set; }
        public int TotalVote { get; set; }
    }
}
