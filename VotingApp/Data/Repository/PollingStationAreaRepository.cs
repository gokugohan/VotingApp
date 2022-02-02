using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Data.Interface;

namespace VotingApp.Data.Repository
{
    public class PollingStationAreaRepository : RepositoryBase<PollingStationArea>, IPollingStationAreaRepository
    {
        public PollingStationAreaRepository(AppDbContext context) : base(context)
        {
        }

        public IEnumerable<PollingStationArea> GetAllWithAreaPollingStation()
        {
            return this.Context.PollingStationArea.Include(m => m.Area).Include(m=>m.PollingStation).ToList();
        }
        public async Task<IEnumerable<PollingStationArea>> GetAllWithAreaPollingStationAsync()
        {
            return await this.Context.PollingStationArea.Include(m => m.Area).Include(m => m.PollingStation).ToListAsync();
        }

        public PollingStationArea GetWithArea(Guid? id)
        {
            return this.Context.PollingStationArea.Include(m => m.Area).FirstOrDefault(m => m.Id.Equals(id));
        }
        public async Task<PollingStationArea> GetWithAreaAsync(Guid? id)
        {
            return await this.Context.PollingStationArea.Include(m => m.Area).FirstOrDefaultAsync(m => m.Id.Equals(id));
        }

        public List<Area> GetPollingStationWithinMunicipality(string municipalityId)
        {
            var municipalityPollingStationArea = (from psa in this.Context.PollingStationArea
                                                  from a in this.Context.Areas
                                                  from ps in this.Context.PollingStations
                                                  where psa.AreaId == a.Id & a.ParentId.Substring(0, 6).Equals(municipalityId) & 
                                                        ps.Id.Equals(psa.PollingStationId)

                                                  select new Area
                                                  {
                                                      Id = a.Id,
                                                      Name = $"{a.Name} | {ps.Name}",
                                                      Level = a.Level
                                                  }).ToList();

            return municipalityPollingStationArea;
        }
    }
}
