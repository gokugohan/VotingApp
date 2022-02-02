using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Models
{
    public class CreateVotingAreaModel
    {
        public Guid Id { get; set; }
        public string AreaId { get; set; }



        public string Description { get; set; }
    }
}
