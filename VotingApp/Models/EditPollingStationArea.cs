using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Data;

namespace VotingApp.Models
{
    public class EditPollingStationArea
    {
        public Guid Id { get; set; }

        public Guid PollingStationId { get; set; }
        public PollingStation PollingStation { get; set; }

        public string AreaId { get; set; }
        public Area Area { get; set; }

        public string StationName { get; set; }

    }
}
