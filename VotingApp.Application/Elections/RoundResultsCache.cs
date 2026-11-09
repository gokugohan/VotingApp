using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace VotingApp.Application.Elections
{
    /// <summary>
    /// Cache for everything derived from a round's results (summaries, breakdowns, station lists, CSV).
    /// Built for results night, when thousands of visitors refresh while sheets are being approved:
    /// <list type="bullet">
    /// <item><b>Single flight</b>: per key, only one request rebuilds at a time, so an expiry or an
    /// approval never sends every concurrent request to the database at once (cache stampede).</item>
    /// <item><b>Stale while revalidating</b>: while one request rebuilds, the others are served the
    /// previous value immediately instead of queueing. Only a cold key (never built) makes callers wait.</item>
    /// <item><b>Versioned invalidation</b>: <see cref="Invalidate"/> marks every entry of a round stale;
    /// the next request rebuilds. A request with no concurrent rebuild always gets fresh data.</item>
    /// </list>
    /// Registered as a singleton; values must be immutable.
    /// </summary>
    public sealed class RoundResultsCache
    {
        private sealed record Entry(object Value, long Version, DateTime BuiltAtUtc);

        private readonly ConcurrentDictionary<string, Entry> entries = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new();
        private readonly ConcurrentDictionary<Guid, long> versions = new();

        /// <summary>Entries older than this are rebuilt even without an invalidation.</summary>
        public TimeSpan MaxAge { get; init; } = TimeSpan.FromSeconds(30);

        public void Invalidate(Guid roundId) => versions.AddOrUpdate(roundId, 1, (_, v) => v + 1);

        public async Task<T> GetOrBuildAsync<T>(Guid roundId, string key, Func<Task<T>> build)
        {
            var fullKey = $"{roundId}:{key}";
            var version = versions.GetOrAdd(roundId, 0);

            if (entries.TryGetValue(fullKey, out var entry) && IsFresh(entry, version))
            {
                return (T)entry.Value;
            }

            var gate = gates.GetOrAdd(fullKey, _ => new SemaphoreSlim(1, 1));
            if (entry != null && !await gate.WaitAsync(0))
            {
                // Someone is already rebuilding: serve the previous value rather than queue.
                return (T)entry.Value;
            }
            if (entry == null)
            {
                await gate.WaitAsync(); // cold key: wait for the first build instead of building in parallel
            }

            try
            {
                version = versions.GetOrAdd(roundId, 0);
                if (entries.TryGetValue(fullKey, out entry) && IsFresh(entry, version))
                {
                    return (T)entry.Value; // built by the request we waited for
                }

                var value = await build();
                // Record the version read *before* building: an invalidation during the build leaves it stale.
                entries[fullKey] = new Entry(value, version, DateTime.UtcNow);
                return value;
            }
            finally
            {
                gate.Release();
            }
        }

        private bool IsFresh(Entry entry, long currentVersion) =>
            entry.Version == currentVersion && DateTime.UtcNow - entry.BuiltAtUtc < MaxAge;
    }
}
