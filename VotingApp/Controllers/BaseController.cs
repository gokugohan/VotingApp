using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Data;
using VotingApp.Data.Interface;
using VotingApp.Helpers;
using VotingApp.SignalRHub;
using static VotingApp.Helpers.Helper;

namespace VotingApp.Controllers
{
    [Authorize]
    public class BaseController : Controller
    {
        protected readonly IRepositoryWrapper repository;
        protected readonly ILogger<BaseController> logger;
        protected readonly IHubContext<VotingHub> hubContext;
        protected readonly IMapper mapper;
        public BaseController(IRepositoryWrapper repository, IHubContext<VotingHub> hubContext, IMapper mapper, ILogger<BaseController> logger) : base()
        {
            this.logger = logger;
            this.hubContext = hubContext;
            this.repository = repository;
            this.mapper = mapper;
        }


        protected static IList<PermissionItem> GetPermissions()
        {
            return Permission.List().OrderBy(m => m.Description).ToList();
        }


    }
}
