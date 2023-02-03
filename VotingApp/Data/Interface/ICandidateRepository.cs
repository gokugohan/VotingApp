using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Data.Interface
{
    public interface ICandidateRepository:IRepositoryBase<Candidate>
    {
        Task<bool> hasAnyVoting(Guid candidateId);
        Task<int> GetTotalVoting(Guid candidateId);
        Task<IList<Vote>> getTotalVotingWithAreas(Guid candidateId);
    }
}
