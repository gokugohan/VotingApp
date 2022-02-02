using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Data.Interface;
using VotingApp.Data.ModelAux;

namespace VotingApp.Data.Repository
{
    public class VoteRepository : RepositoryBase<Vote>, IVoteRepository
    {
        public VoteRepository(AppDbContext context) : base(context)
        {
        }


        public bool IsVoteAlreadyExist(Guid candidateId, Guid pollingStationAreaId)
        {
            return this.Context.Votes.Any(m => m.CandidateId.Equals(candidateId) && m.PollingStationAreaId.Equals(pollingStationAreaId));
        }

        /// <summary>
        /// Get total votes of candidates
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, int> TotalVoteOfCandidate()
        {

            var votes = this.Context.Votes.Include(m => m.Candidate).GroupBy(m => m.Candidate.Name).Select(m => new
            {
                m.Key,
                SUM = m.Sum(m => m.Total)
            });

            var dictionary = new Dictionary<string, int>();
            foreach (var item in votes)
            {
                dictionary.Add(item.Key, item.SUM);
            }
            return dictionary;

        }
        public async Task<Dictionary<string, VotingByMunicipality>> ListVotingInMunicipality()
        {

            var votings = this.Context.Votes
                .Include(m => m.Candidate)
                .Include(m => m.PollingStationArea).ThenInclude(m => m.Area)
                .Include(m => m.PollingStationArea).ThenInclude(m => m.PollingStation)
                .ToList();


            var dictionaryVoting = new Dictionary<string, VotingByMunicipality>();

            var municipalities = await this.Context.Areas.Where(m => m.Level == 2).OrderBy(m=>m.Name).ToListAsync();

            municipalities.ForEach(m =>
            {
                dictionaryVoting.Add(m.Name, null);
            });

            foreach (var voting in votings)
            {

                var area_id = voting.PollingStationArea.AreaId.Substring(0, 6);

                var area = await this.Context.Areas.FirstOrDefaultAsync(m => m.Id.Equals(area_id));
                if (area != null)
                {
                    var item = dictionaryVoting[area.Name];
                    if (item == null)
                    {
                        item = new VotingByMunicipality
                        {
                            AreaId = area.Id,
                            AreaName = area.Name,
                            TotalVote = voting.Total
                        };
                    }
                    else
                    {
                        item.TotalVote += voting.Total;
                    }

                    dictionaryVoting[area.Name] = item;
                }

            }

            return dictionaryVoting;


        }
        public async Task<Dictionary<string, VotingByMunicipality>> ListVotingInMunicipalityByCandidateId(Guid? candidateId)
        {
            if (!candidateId.HasValue)
            {
                return await ListVotingInMunicipality();

            }

            var votings = this.Context.Votes
                .Where(m => m.CandidateId.Equals(candidateId))
                .Include(m => m.Candidate)
                .Include(m => m.PollingStationArea).ThenInclude(m => m.Area)
                .Include(m => m.PollingStationArea).ThenInclude(m => m.PollingStation)
                .ToList();


            var dictionaryVoting = new Dictionary<string, VotingByMunicipality>();

            var municipalities = await this.Context.Areas.Where(m => m.Level == 2).OrderBy(m=>m.Name).ToListAsync();

            municipalities.ForEach(m =>
            {
                dictionaryVoting.Add(m.Name, null);
            });

            foreach (var voting in votings)
            {

                var area_id = voting.PollingStationArea.AreaId.Substring(0, 6);

                var area = await this.Context.Areas.FirstOrDefaultAsync(m => m.Id.Equals(area_id));
                if (area != null)
                {
                    var item = dictionaryVoting[area.Name];
                    if (item == null)
                    {
                        item = new VotingByMunicipality
                        {
                            AreaId = area.Id,
                            AreaName = area.Name,
                            TotalVote = voting.Total
                        };
                    }
                    else
                    {
                        item.TotalVote += voting.Total;
                    }

                    dictionaryVoting[area.Name] = item;
                }

            }


            return dictionaryVoting;


        }




        public async Task<Dictionary<string, VotingCandidate>> GetCandidateVotesWithinMunicipality(string id)
        {
            
            var votings = await this.Context.Votes
                .Where(m=>m.PollingStationArea.AreaId.Substring(0,6).Equals(id))
                .Include(m => m.Candidate)
                .Include(m => m.PollingStationArea).ThenInclude(m => m.Area)
                .Include(m => m.PollingStationArea).ThenInclude(m => m.PollingStation)
                .ToListAsync();

            var list = new List<VotingCandidate>();
            var candidates = await this.Context.Candidates.OrderBy(m=>m.Name).ToListAsync();
            var dictionaryVoting = new Dictionary<string, VotingCandidate>();

            candidates.ForEach(c => { dictionaryVoting.Add(c.Name, null); });

            votings.ForEach(v =>
            {

                var dictionary = dictionaryVoting[v.Candidate.Name];
                if (dictionary == null)
                {
                    var vCandidate = new VotingCandidate
                    {
                        CandidateId = v.CandidateId,
                        CandidateName = v.Candidate.Name,
                        TotalVote = v.Total,
                    };
                    dictionary = vCandidate;
                    //list.Add(vCandidate);
                }
                else
                {
                    dictionary.TotalVote += v.Total;
                }

                dictionaryVoting[v.Candidate.Name] = dictionary;


            });

            return dictionaryVoting;

        }


        public async Task<List<CandidateVoteListMunicipality>> GetCandidateMunicipalitVotes()
        {

            var candidates = await this.Context.Candidates.ToListAsync();
            var list = new List<CandidateVoteListMunicipality>();
            foreach (var candidate in candidates)
            {
                var res = await GetCandidateMunicipalityVotesAsync(candidate.Id);
                list.Add(res);
            }
            return list;
        }

        private async Task<CandidateVoteListMunicipality> GetCandidateMunicipalityVotesAsync(Guid candidateId)
        {
            var votings = this.Context.Votes
                            .Where(m => m.CandidateId.Equals(candidateId))
                            .Include(m => m.Candidate)
                            .Include(m => m.PollingStationArea).ThenInclude(m => m.Area)
                            .Include(m => m.PollingStationArea).ThenInclude(m => m.PollingStation)
                            .ToList();

            var candidate = await this.Context.Candidates.FindAsync(candidateId);

            var dictionaryVoting = new Dictionary<string, VotingByMunicipality>();

            var municipalities = await this.Context.Areas.Where(m => m.Level == 2).OrderBy(m=>m.Name).ToListAsync();

            municipalities.ForEach(m =>
            {
                dictionaryVoting.Add(m.Name, null);
            });

            var ret = new List<int>();

            foreach (var voting in votings)
            {

                var area_id = voting.PollingStationArea.AreaId.Substring(0, 6);

                var area = await this.Context.Areas.FirstOrDefaultAsync(m => m.Id.Equals(area_id));
                if (area != null)
                {
                    var item = dictionaryVoting[area.Name];
                    if (item == null)
                    {
                        item = new VotingByMunicipality
                        {
                            AreaId = area.Id,
                            AreaName = area.Name,
                            TotalVote = voting.Total
                        };
                    }
                    else
                    {
                        item.TotalVote += voting.Total;
                    }

                    dictionaryVoting[area.Name] = item;
                }

            }
            foreach (var item in dictionaryVoting)
            {
                ret.Add((item.Value == null) ? 0 : item.Value.TotalVote);

            }

            return new CandidateVoteListMunicipality
            {
                Candidate = candidate,
                Votes = ret
            };
        }

        public int GetTotalVote()
        {
            return this.Context.Votes.Sum(m => m.Total);
        }

        public IEnumerable<Vote> GetAllWithEntities()
        {
            return this.Context.Votes.Include(v => v.Candidate).Include(v => v.PollingStationArea).ThenInclude(m => m.Area).ToList();
        }

        public async Task<IEnumerable<Vote>> GetAllWithEntitiesAsync()
        {
            return await this.Context.Votes.Include(v => v.Candidate).Include(v => v.PollingStationArea).ThenInclude(m => m.Area).ToListAsync();
        }

        public Vote GetWithAllEntities(Guid? id)
        {
            return this.Context.Votes.Include(v => v.Candidate).Include(v => v.PollingStationArea).ThenInclude(m => m.Area).FirstOrDefault(m => m.Id.Equals(id));
        }

        public async Task<Vote> GetWithAllEntitiesAsync(Guid? id)
        {
            return await this.Context.Votes.Include(v => v.Candidate).Include(v => v.PollingStationArea).ThenInclude(m => m.Area).FirstOrDefaultAsync(m => m.Id.Equals(id));
        }

    }
}
