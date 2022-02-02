using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Data.Interface
{
    public interface IRepositoryWrapper
    {
        IAreaRepository Areas { get; }
        ICandidateRepository Candidates { get; }
        IPollingStationAreaRepository PollingStationAreas { get; }
        IPollingStationRepository PollingStations { get; }
        IVoteRepository Votes { get; }
        bool Save();
        Task<bool> SaveAsync();
        void Rollback();
        Task RollbackAsync();
    }
}
