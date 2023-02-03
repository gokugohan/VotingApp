using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Data.Interface;

namespace VotingApp.Data.Repository
{
    public class CandidateRepository : RepositoryBase<Candidate>, ICandidateRepository
    {
        public CandidateRepository(AppDbContext context) : base(context)
        {
        }


        public async Task<bool> hasAnyVoting(Guid candidateId)
        {
            return await this.Context.Votes.Where(m => m.CandidateId.Equals(candidateId)).AnyAsync();

        }

        public async Task<int> GetTotalVoting(Guid candidateId)
        {
            int total = 0;
            var result = await this.Context.Votes.Where(m => m.CandidateId.Equals(candidateId)).ToListAsync();
            foreach(var item in result)
            {
                total += item.Total;

            }

            return total;
        }

        public async Task<IList<Vote>> getTotalVotingWithAreas(Guid candidateId)
        {

            var result = await this.Context.Votes.Where(m => m.CandidateId.Equals(candidateId))
                .Include(m=>m.Candidate)
                .Include(m=>m.PollingStationArea).ThenInclude(m=>m.Area)
                .Include(m=>m.PollingStationArea).ThenInclude(m=>m.PollingStation)
                .ToListAsync();

            return result;

        }
    }
}
