using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Data.Interface;
using VotingApp.Helpers;

namespace VotingApp.Data.Repository
{
    public class AreaRepository:RepositoryBase<Area>, IAreaRepository
    {
        public AreaRepository(AppDbContext context) : base(context)
        {
        }


    }
}
