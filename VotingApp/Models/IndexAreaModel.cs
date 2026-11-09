using System.Collections.Generic;
using VotingApp.Domain.Entities;

namespace VotingApp.Models
{
    public class IndexAreaModel
    {
        public ICollection<Area> Municipalities { get; set; }
        public ICollection<Area> AdministrativePosts { get; set; }
        public ICollection<Area> Villages { get; set; }

    }
}
