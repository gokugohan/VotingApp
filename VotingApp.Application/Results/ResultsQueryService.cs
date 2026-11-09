using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VotingApp.Application.Abstractions;
using VotingApp.Application.Geography;
using VotingApp.Domain.Entities;

namespace VotingApp.Application.Results
{
    /// <summary>
    /// Read-only aggregates for the public dashboard (map, charts, totals), computed from the
    /// legacy Votes table. Each method is a single grouped query; the result shapes match what the
    /// existing dashboard scripts expect.
    /// </summary>
    public class ResultsQueryService
    {
        private const int CodeLength = AreaHierarchy.MunicipalityCodeLength;
        private readonly IAppDbContext db;

        public ResultsQueryService(IAppDbContext db) => this.db = db;

        public async Task<int> GetTotalVotesAsync(CancellationToken ct = default) =>
            await db.Votes.SumAsync(v => (int?)v.Total, ct) ?? 0;

        /// <summary>Total votes per candidate name.</summary>
        public async Task<Dictionary<string, int>> GetTotalsByCandidateAsync(CancellationToken ct = default)
        {
            var rows = await db.Votes
                .GroupBy(v => v.Candidate.Name)
                .Select(g => new { Name = g.Key, Total = g.Sum(v => v.Total) })
                .ToListAsync(ct);
            return rows.Where(r => r.Name != null).ToDictionary(r => r.Name, r => r.Total);
        }

        /// <summary>
        /// Votes per municipality (all candidates, or one), keyed by municipality name in name order.
        /// Municipalities without votes map to null.
        /// </summary>
        public async Task<Dictionary<string, VotingByMunicipality>> GetTotalsByMunicipalityAsync(Guid? candidateId, CancellationToken ct = default)
        {
            var votes = db.Votes.AsQueryable();
            if (candidateId.HasValue) votes = votes.Where(v => v.CandidateId == candidateId.Value);

            var totals = await votes
                .GroupBy(v => v.PollingStationArea.AreaId.Substring(0, CodeLength))
                .Select(g => new { MunicipalityId = g.Key, Total = g.Sum(v => v.Total) })
                .ToDictionaryAsync(x => x.MunicipalityId, x => x.Total, ct);

            var result = new Dictionary<string, VotingByMunicipality>();
            foreach (var m in await MunicipalitiesAsync(ct))
            {
                if (result.ContainsKey(m.Name)) continue;
                result[m.Name] = totals.TryGetValue(m.Id, out var total)
                    ? new VotingByMunicipality { AreaId = m.Id, AreaKey = MapKeys.ForMunicipality(m.Id), AreaName = m.Name, TotalVote = total }
                    : null;
            }
            return result;
        }

        /// <summary>Votes per candidate within one municipality, keyed by candidate name; null when none.</summary>
        public async Task<Dictionary<string, VotingCandidate>> GetCandidateTotalsInMunicipalityAsync(string municipalityId, CancellationToken ct = default)
        {
            municipalityId ??= string.Empty;
            var totals = municipalityId.Length != CodeLength ? new Dictionary<Guid, int>() : await db.Votes
                .Where(v => v.PollingStationArea.AreaId.StartsWith(municipalityId))
                .GroupBy(v => v.CandidateId)
                .Select(g => new { CandidateId = g.Key, Total = g.Sum(v => v.Total) })
                .ToDictionaryAsync(x => x.CandidateId, x => x.Total, ct);

            var candidates = await db.Candidates.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
            var result = new Dictionary<string, VotingCandidate>();
            foreach (var c in candidates)
            {
                var name = c.Name ?? string.Empty;
                result.TryGetValue(name, out var existing);
                if (!totals.TryGetValue(c.Id, out var total))
                {
                    if (!result.ContainsKey(name)) result[name] = null;
                    continue;
                }
                // Candidates sharing a name are merged, as before.
                result[name] = existing == null
                    ? new VotingCandidate { CandidateId = c.Id, CandidateName = c.Name, TotalVote = total }
                    : new VotingCandidate { CandidateId = existing.CandidateId, CandidateName = c.Name, TotalVote = existing.TotalVote + total };
            }
            return result;
        }

        /// <summary>For each candidate: votes per municipality, in municipality-name order (chart series).</summary>
        public async Task<List<CandidateVoteListMunicipality>> GetCandidateSeriesByMunicipalityAsync(CancellationToken ct = default)
        {
            var totals = await db.Votes
                .GroupBy(v => new { v.CandidateId, MunicipalityId = v.PollingStationArea.AreaId.Substring(0, CodeLength) })
                .Select(g => new { g.Key.CandidateId, g.Key.MunicipalityId, Total = g.Sum(v => v.Total) })
                .ToListAsync(ct);
            var lookup = totals.ToDictionary(t => (t.CandidateId, t.MunicipalityId), t => t.Total);

            var municipalities = (await MunicipalitiesAsync(ct)).GroupBy(m => m.Name).Select(g => g.First()).ToList();
            var candidates = await db.Candidates.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);

            return candidates.Select(c => new CandidateVoteListMunicipality
            {
                Candidate = c,
                Votes = municipalities.Select(m => lookup.TryGetValue((c.Id, m.Id), out var t) ? t : 0).ToList(),
            }).ToList();
        }

        /// <summary>Each vote row in a municipality, with candidate, village and map key.</summary>
        public async Task<List<CandidateMunicipalityVoteItem>> GetVotesInMunicipalityAsync(string municipalityId, CancellationToken ct = default)
        {
            if (municipalityId?.Length != CodeLength) return new List<CandidateMunicipalityVoteItem>();
            var rows = await db.Votes.AsNoTracking()
                .Where(v => v.PollingStationArea.AreaId.StartsWith(municipalityId))
                .Select(v => new { v.Candidate, v.PollingStationArea.Area, v.Total })
                .ToListAsync(ct);
            var key = MapKeys.ForMunicipality(municipalityId);
            return rows.Select(r => new CandidateMunicipalityVoteItem { Candidate = r.Candidate, Area = r.Area, AreaKey = key, Total = r.Total }).ToList();
        }

        private Task<List<Area>> MunicipalitiesAsync(CancellationToken ct) =>
            db.Areas.AsNoTracking().Where(a => a.Level == AreaHierarchy.Municipality).OrderBy(a => a.Name).ToListAsync(ct);
    }
}
