using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VotingApp.Application.Elections;

namespace VotingApp.Tests
{
    public class RoundResultsCacheTests
    {
        private static readonly Guid Round = Guid.NewGuid();

        [Fact]
        public async Task Concurrent_cold_requests_build_once()
        {
            var cache = new RoundResultsCache();
            var builds = 0;
            async Task<int> Build()
            {
                Interlocked.Increment(ref builds);
                await Task.Delay(100);
                return 42;
            }

            var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cache.GetOrBuildAsync(Round, "k", Build)));

            Assert.All(results, r => Assert.Equal(42, r));
            Assert.Equal(1, builds);
        }

        [Fact]
        public async Task During_a_rebuild_other_requests_get_the_previous_value_immediately()
        {
            var cache = new RoundResultsCache();
            await cache.GetOrBuildAsync(Round, "k", () => Task.FromResult("v1"));
            cache.Invalidate(Round);

            var release = new TaskCompletionSource();
            var rebuilding = cache.GetOrBuildAsync(Round, "k", async () => { await release.Task; return "v2"; });

            // While the rebuild is blocked, another caller is not queued behind it.
            var other = await cache.GetOrBuildAsync(Round, "k", () => Task.FromResult("never"));
            Assert.Equal("v1", other);

            release.SetResult();
            Assert.Equal("v2", await rebuilding);
            Assert.Equal("v2", await cache.GetOrBuildAsync(Round, "k", () => Task.FromResult("never")));
        }

        [Fact]
        public async Task Invalidation_affects_only_that_round()
        {
            var cache = new RoundResultsCache();
            var other = Guid.NewGuid();
            await cache.GetOrBuildAsync(Round, "k", () => Task.FromResult(1));
            await cache.GetOrBuildAsync(other, "k", () => Task.FromResult(1));

            cache.Invalidate(Round);

            Assert.Equal(2, await cache.GetOrBuildAsync(Round, "k", () => Task.FromResult(2)));
            Assert.Equal(1, await cache.GetOrBuildAsync(other, "k", () => Task.FromResult(2)));
        }

        [Fact]
        public async Task A_failed_build_is_not_cached()
        {
            var cache = new RoundResultsCache();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                cache.GetOrBuildAsync<int>(Round, "k", () => throw new InvalidOperationException()));
            Assert.Equal(7, await cache.GetOrBuildAsync(Round, "k", () => Task.FromResult(7)));
        }
    }
}
