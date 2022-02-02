using System.Collections.Generic;
using VotingApp.Data;

namespace VotingApp.Models
{
    public class IndexAreaModel
    {
        public ICollection<Area> Municipalities { get; set; }
        public ICollection<Area> AdministrativePosts { get; set; }
        public ICollection<Area> Villages { get; set; }

    }
}
