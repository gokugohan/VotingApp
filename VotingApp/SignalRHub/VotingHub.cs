using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.SignalRHub
{
    public class VotingHub : Hub
    {
        public async Task BroadcastVotingData(string message) => await this.Clients.All.SendAsync("OnbroadcastVotingDataListener",message);
    }
}
