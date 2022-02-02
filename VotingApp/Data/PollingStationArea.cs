using System;
using System.ComponentModel.DataAnnotations;

namespace VotingApp.Data
{
    public class PollingStationArea
    {
        public Guid Id { get; set; }

        [Required]
        public Guid PollingStationId { get; set; }
        public PollingStation PollingStation { get; set; }

        [Required]
        public string AreaId { get; set; }
        public Area Area { get; set; }

        //public string Description { get; set; }
    }
}
