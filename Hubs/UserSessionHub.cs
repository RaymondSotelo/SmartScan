using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SmartScan.Hubs
{
    [Authorize]
    public class UserSessionHub : Hub
    {
        // SignalR automatically maps connected users by User.FindFirstValue(ClaimTypes.NameIdentifier)
    }
}
