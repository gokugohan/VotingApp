using Microsoft.AspNetCore.SignalR;

namespace VotingApp.SignalRHub
{
    /// <summary>
    /// Push-only hub: clients subscribe to <see cref="VotingDataEvent"/>; only the server broadcasts
    /// (via IHubContext after a successful save). No client-invocable methods, so anonymous
    /// viewers cannot push messages to everyone.
    /// </summary>
    public class VotingHub : Hub
    {
        public const string VotingDataEvent = "OnbroadcastVotingDataListener";
    }
}
