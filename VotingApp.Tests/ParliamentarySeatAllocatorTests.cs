using System;
using System.Linq;
using VotingApp.Domain.Elections.Rules;

namespace VotingApp.Tests
{
    public class ParliamentarySeatAllocatorTests
    {
        private readonly ParliamentarySeatAllocator allocator = new();

        private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
        private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");
        private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000c");
        private static readonly Guid D = Guid.Parse("00000000-0000-0000-0000-00000000000d");

        private static int Seats(ParliamentaryResult r, Guid id) => r.Parties.Single(p => p.ContestantId == id).Seats;

        [Fact]
        public void Textbook_dhondt_example_without_threshold()
        {
            // Quotients: A 100k 50k 33.3k 25k | B 80k 40k 26.7k | C 30k | D 20k  -> 4 / 3 / 1 / 0
            var r = allocator.Allocate(new[]
            {
                new ContestantVotes(A, 100_000), new ContestantVotes(B, 80_000),
                new ContestantVotes(C, 30_000), new ContestantVotes(D, 20_000),
            }, seats: 8, thresholdPercent: 0);

            Assert.Equal(4, Seats(r, A));
            Assert.Equal(3, Seats(r, B));
            Assert.Equal(1, Seats(r, C));
            Assert.Equal(0, Seats(r, D));
            Assert.Equal(A, r.LastSeatWinnerId); // 8th quotient is A's 25k
            Assert.False(r.LastSeatDecidedByTie);
        }

        [Fact]
        public void All_65_seats_are_allocated_among_parties_over_threshold()
        {
            var r = allocator.Allocate(new[]
            {
                new ContestantVotes(A, 280_000), new ContestantVotes(B, 170_000),
                new ContestantVotes(C, 60_000), new ContestantVotes(D, 20_000), // D: 3.77% < 4%
            });

            Assert.Equal(65, r.Parties.Sum(p => p.Seats));
            Assert.False(r.Parties.Single(p => p.ContestantId == D).MeetsThreshold);
            Assert.Equal(0, Seats(r, D));
        }

        [Fact]
        public void Party_exactly_at_threshold_qualifies()
        {
            // C has exactly 4% of 1,000,000 valid votes.
            var r = allocator.Allocate(new[]
            {
                new ContestantVotes(A, 600_000), new ContestantVotes(B, 360_000), new ContestantVotes(C, 40_000),
            });

            Assert.True(r.Parties.Single(p => p.ContestantId == C).MeetsThreshold);
            Assert.True(Seats(r, C) > 0);
        }

        [Fact]
        public void Threshold_uses_total_valid_votes_including_parties_not_listed()
        {
            // 40,000 of 1,040,000 valid votes is 3.85%: below threshold once the extra valid votes count.
            var r = allocator.Allocate(new[]
            {
                new ContestantVotes(A, 600_000), new ContestantVotes(B, 360_000), new ContestantVotes(C, 40_000),
            }, totalValidVotes: 1_040_000);

            Assert.False(r.Parties.Single(p => p.ContestantId == C).MeetsThreshold);
        }

        [Fact]
        public void Tie_for_last_seat_goes_to_party_with_fewer_votes_and_is_flagged()
        {
            // 3 seats. A=6000 (q: 6000, 3000, 2000), B=3000 (q: 3000).
            // Seat 1: A(6000). Seat 2: A 3000 vs B 3000 tie -> B (fewer votes). Seat 3: A 3000.
            // Use 2 seats so the tie decides the last seat.
            var r = allocator.Allocate(new[] { new ContestantVotes(A, 6000), new ContestantVotes(B, 3000) },
                seats: 2, thresholdPercent: 0);

            Assert.Equal(1, Seats(r, A));
            Assert.Equal(1, Seats(r, B));
            Assert.Equal(B, r.LastSeatWinnerId);
            Assert.True(r.LastSeatDecidedByTie);
        }

        [Fact]
        public void Next_in_line_is_party_with_highest_remaining_quotient()
        {
            var r = allocator.Allocate(new[]
            {
                new ContestantVotes(A, 100_000), new ContestantVotes(B, 80_000),
                new ContestantVotes(C, 30_000), new ContestantVotes(D, 20_000),
            }, seats: 8, thresholdPercent: 0);

            // Remaining quotients: A 20k, B 20k, C 15k, D 20k -> tie at 20k, fewer votes first -> D.
            Assert.Equal(D, r.NextInLineId);
        }

        [Fact]
        public void No_votes_allocates_nothing()
        {
            var r = allocator.Allocate(new[] { new ContestantVotes(A, 0), new ContestantVotes(B, 0) });
            Assert.All(r.Parties, p => Assert.Equal(0, p.Seats));
            Assert.Null(r.LastSeatWinnerId);
        }

        [Fact]
        public void Rejects_negative_votes_and_duplicates()
        {
            Assert.Throws<ArgumentException>(() => allocator.Allocate(new[] { new ContestantVotes(A, -1) }));
            Assert.Throws<ArgumentException>(() => allocator.Allocate(new[] { new ContestantVotes(A, 1), new ContestantVotes(A, 2) }));
        }
    }
}
