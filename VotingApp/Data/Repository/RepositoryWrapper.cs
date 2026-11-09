using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using VotingApp.Data.Interface;

namespace VotingApp.Data.Repository
{
    public class RepositoryWrapper : IRepositoryWrapper
    {
        private readonly AppDbContext Context;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly ILogger<RepositoryWrapper> logger;

        private IAreaRepository _areaRepository;
        private ICandidateRepository _candidateRepository;
        private IPollingStationAreaRepository _pollingStationAreaRepository;
        private IPollingStationRepository _pollingStationRepository;
        private IVoteRepository _voteRepository;

        public RepositoryWrapper(AppDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<RepositoryWrapper> logger)
        {
            this.Context = context;
            this.httpContextAccessor = httpContextAccessor;
            this.logger = logger;
        }

        public IAreaRepository Areas => this._areaRepository ??= new AreaRepository(this.Context);

        public ICandidateRepository Candidates => this._candidateRepository ??= new CandidateRepository(this.Context);

        public IPollingStationAreaRepository PollingStationAreas => this._pollingStationAreaRepository ??= new PollingStationAreaRepository(this.Context);

        public IPollingStationRepository PollingStations => this._pollingStationRepository ??= new PollingStationRepository(this.Context);

        public IVoteRepository Votes => this._voteRepository ??= new VoteRepository(this.Context);

        /// <summary>The signed-in user's id, recorded on audit log entries.</summary>
        private string CurrentUserId =>
            this.httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        /// <summary>Discards all pending (unsaved) changes.</summary>
        public void Rollback()
        {
            this.Context.ChangeTracker.Clear();
        }

        public Task RollbackAsync()
        {
            Rollback();
            return Task.CompletedTask;
        }

        public bool Save()
        {
            return SaveAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Saves pending changes (with audit entries) in a transaction.
        /// Returns false and logs the error if the save fails; pending changes are discarded.
        /// </summary>
        public async Task<bool> SaveAsync()
        {
            await using var transaction = await this.Context.Database.BeginTransactionAsync();
            try
            {
                await this.Context.SaveChangesAsync(CurrentUserId);
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Saving changes failed; transaction rolled back");
                await transaction.RollbackAsync();
                Rollback();
                return false;
            }
        }
    }
}
