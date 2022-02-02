using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Data;

namespace VotingApp.Models
{
    public class IndexVotinAreaModel
    {
        public Guid Id { get; set; }
        public string Description { get; set; }
        public Area Village { get; set; }
        public Area PosAdministrative { get; set; }
        public Area Municipality { get; set; }
    }
}
