using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Data;
using VotingApp.Data.Interface;
using VotingApp.SignalRHub;

namespace VotingApp.Controllers
{
    public class AuditLogController :Controller
    {
        private readonly AppDbContext context;
        public AuditLogController(AppDbContext context)
        {
            this.context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await this.context.AuditLogs.OrderByDescending(m=>m.DateTime).ToListAsync());
        }
    }
}
