using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Data.Interface;

namespace VotingApp.Data.Repository
{
    public class RepositoryWrapper : IRepositoryWrapper
    {
        private readonly AppDbContext Context;
        private IAreaRepository _areaRepository;

        private ICandidateRepository _candidateRepository;

        private IPollingStationAreaRepository _pollingStationAreaRepository;
        private IPollingStationRepository _pollingStationRepository;

        private IVoteRepository _voteRepository;

        public RepositoryWrapper(AppDbContext context)
        {
            this.Context = context;

        }
        public IAreaRepository Areas => this._areaRepository ?? new AreaRepository(this.Context);

        public ICandidateRepository Candidates => this._candidateRepository ?? new CandidateRepository(this.Context);

        public IPollingStationAreaRepository PollingStationAreas => this._pollingStationAreaRepository??new PollingStationAreaRepository(this.Context);

        public IPollingStationRepository PollingStations => this._pollingStationRepository??new PollingStationRepository(this.Context);

        public IVoteRepository Votes => this._voteRepository??new VoteRepository(this.Context);

        public void Rollback()
        {
            this.Context.Dispose();
        }

        public async Task RollbackAsync()
        {
            await this.Context.DisposeAsync();
        }

        public bool Save()
        {
            bool returnValue = true;
            using (var dbContextTransaction = this.Context.Database.BeginTransaction())
            {
                try
                {
                    this.Context.SaveChanges();
                    dbContextTransaction.Commit();
                }
                catch (Exception)
                {
                    //Log Exception Handling message                      
                    returnValue = false;
                    dbContextTransaction.Rollback();
                }
            }

            return returnValue;
        }

        public async Task<bool> SaveAsync()
        {
            bool returnValue = true;
            using (var dbContextTransaction = this.Context.Database.BeginTransaction())
            {
                try
                {
                    await this.Context.SaveChangesAsync();
                    await dbContextTransaction.CommitAsync();
                }
                catch (Exception)
                {
                    //Log Exception Handling message                      
                    returnValue = false;
                    await dbContextTransaction.RollbackAsync();
                }
            }

            return returnValue;
        }
    }
}
