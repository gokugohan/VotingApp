using System;

namespace VotingApp.Domain.Elections
{
    /// <summary>
    /// A polling station as it was when the election opened. Elections keep their own copy so that
    /// later changes to stations (renamed, moved to another village, closed, new ones added) never
    /// alter an election that is running or already decided.
    /// </summary>
    public class ElectionStation
    {
        public Guid Id { get; set; }
        public Guid ElectionId { get; set; }
        public Election Election { get; set; }

        /// <summary>The live station this row was copied from (result sheets reference it). No foreign key: history survives deletion.</summary>
        public Guid PollingStationAreaId { get; set; }

        public string StationName { get; set; }
        /// <summary>Village (suco) code at the time.</summary>
        public string AreaId { get; set; }
        public string VillageName { get; set; }
        public string AdministrativePostId { get; set; }
        public string AdministrativePostName { get; set; }
        public string MunicipalityId { get; set; }
        public string MunicipalityName { get; set; }

        public DateTime FrozenAtUtc { get; set; }
    }
}
