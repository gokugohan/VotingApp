using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using VotingApp.Data.ModelAux;

namespace VotingApp.Data.Interface
{
    public interface IVoteRepository:IRepositoryBase<Vote>
    {
        Task<Vote> GetWithAllEntitiesAsync(Guid? id);
        Vote GetWithAllEntities(Guid? id);
        Task<IEnumerable<Vote>> GetAllWithEntitiesAsync();
        Task<IEnumerable<Vote>> GetAllWithEntitiesAsync(Expression<Func<Vote, bool>> expression);
        IEnumerable<Vote> GetAllWithEntities();
        bool IsVoteAlreadyExist(Guid candidateId, Guid pollingStationAreaId);
        Dictionary<string, int> TotalVoteOfCandidate();
        int GetTotalVote();
        Task<Dictionary<string, VotingByMunicipality>> ListVotingInMunicipality();
        Task<Dictionary<string, VotingByMunicipality>> ListVotingInMunicipalityByCandidateId(Guid? candidateId);
        Task<Dictionary<string, VotingCandidate>> GetCandidateVotesWithinMunicipality(string id);
        Task<List<CandidateVoteListMunicipality>> GetCandidateMunicipalitVotes();
        Task<List<CandidateMunicipalityVoteItem>> GetCandidateByMunicipality(string municipalityId);
    }
}
