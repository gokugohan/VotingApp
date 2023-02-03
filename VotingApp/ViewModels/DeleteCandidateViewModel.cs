using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Data;

namespace VotingApp.ViewModels
{
    public class DeleteCandidateViewModel
    {
        public Guid Id { get; set; }
        public Candidate Candidate { get; set; }
        public IList<Vote> VoteList { get; set; }
    }
}
