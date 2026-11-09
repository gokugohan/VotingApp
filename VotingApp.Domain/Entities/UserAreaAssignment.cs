using System;

namespace VotingApp.Domain.Entities
{
    /// <summary>
    /// Limits a user's results work to one municipality. A user with assignments only sees and
    /// edits result sheets in those municipalities; admins and election managers are national.
    /// </summary>
    public class UserAreaAssignment
    {
        public Guid Id { get; set; }

        /// <summary>Identity user id.</summary>
        public string UserId { get; set; }

        public string MunicipalityId { get; set; }
        public Area Municipality { get; set; }
    }
}
