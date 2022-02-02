using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Data.Interface;

namespace VotingApp.Data.Repository
{
    public class PollingStationRepository:RepositoryBase<PollingStation>, IPollingStationRepository
    {
        public PollingStationRepository(AppDbContext context) : base(context)
        {
        }
    }
}
