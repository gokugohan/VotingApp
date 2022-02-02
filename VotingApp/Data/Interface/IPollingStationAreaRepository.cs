using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Data.Interface
{
    public interface IPollingStationAreaRepository:IRepositoryBase<PollingStationArea>
    {
        IEnumerable<PollingStationArea> GetAllWithAreaPollingStation();
        Task<IEnumerable<PollingStationArea>> GetAllWithAreaPollingStationAsync();
        List<Area> GetPollingStationWithinMunicipality(string municipalityId);
        PollingStationArea GetWithArea(Guid? id);
        Task<PollingStationArea> GetWithAreaAsync(Guid? id);
    }
}
