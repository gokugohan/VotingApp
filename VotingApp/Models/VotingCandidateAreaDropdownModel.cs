using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Models
{
    public class VotingCandidateAreaDropdownModel
    {
        public Guid Id { get; set; }
        //public Guid CandidateId { get; set; }
        //public string CandidateName { get; set; }
        public Guid VotingAreaId { get; set; }
        public string VotingAreaName { get; set; }
        //public int Total { get; set; }
    }
}
