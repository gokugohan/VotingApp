using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Models
{
    public class SaveCandidateVote
    {
        public Guid CandidateId { get; set; }
        public Guid PollingStationAreaId { get; set; }
        public int Total { get; set; }
    }
}
