using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace TrollerRelay.Hubs
{
    public class ScreenHub : Hub
    {
        // Client sends its latest frame as base64 or byte array
        // We broadcast it to everyone in the "Viewers" group.
        public async Task SendFrame(string clientId, string base64Image)
        {
            await Clients.Group("Viewers").SendAsync("ReceiveFrame", clientId, base64Image);
        }

        // The desktop viewer joins the Viewers group
        public async Task JoinAsViewer()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Viewers");
        }
        
        public async Task LeaveAsViewer()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Viewers");
        }

        // Command to tell clients to start streaming
        public async Task RequestStreamStart(string targetClientId)
        {
             // Broadcast command to client
             await Clients.All.SendAsync("CommandStartStream", targetClientId);
        }

        public async Task RequestStreamStop(string targetClientId)
        {
             await Clients.All.SendAsync("CommandStopStream", targetClientId);
        }
    }
}
