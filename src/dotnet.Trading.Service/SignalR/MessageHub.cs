using System.Threading.Tasks;
using dotnet.Trading.Service.StateMachines;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace dotnet.Trading.Service.SignalR
{
    [Authorize]
    public class MessageHub : Hub
    {
        public async Task SendStatusAsync(PurchaseState status)
        {
            if (Clients != null)
            {
                await Clients.User(Context.UserIdentifier)
                            .SendAsync("ReceivePurchaseStatus", status);
            }
        }
    }
}