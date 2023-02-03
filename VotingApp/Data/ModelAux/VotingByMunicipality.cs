using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Data.ModelAux
{
    public class VotingByMunicipality
    {
        public string AreaId { get; set; }
        public string AreaKey { get; set; }
        public string AreaName { get; set; }
        public int TotalVote { get; set; }

    }
}
