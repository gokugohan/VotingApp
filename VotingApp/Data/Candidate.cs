using System;

namespace VotingApp.Data
{
    public class Candidate
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Avatar { get; set; }

    }
}
