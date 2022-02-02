using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VotingApp.Data
{
    public class Vote
    {
        [Key]
        //[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public Guid CandidateId { get; set; }
        public Candidate Candidate { get; set; }


        public Guid PollingStationAreaId { get; set; }
        public PollingStationArea PollingStationArea { get; set; }
        public int Total { get; set; }
    }
}
