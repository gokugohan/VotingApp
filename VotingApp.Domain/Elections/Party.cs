using System;

namespace VotingApp.Domain.Elections
{
    public enum PartyKind
    {
        Party = 1,
        Coalition = 2,
    }

    /// <summary>
    /// A political party or coalition as a lasting record across elections. Each election's
    /// <see cref="Contestant"/> links to it, so results can be compared from one election to the next.
    /// </summary>
    public class Party
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Acronym { get; set; }
        public PartyKind Kind { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
