namespace DhlLogistics.Web.Hub;

using Microsoft.AspNetCore.SignalR;

public class NotificationHub : Hub
{
    // Each browser/mobile client calls this after connecting.
    // Groups: "user-{id}" for targeted pushes, "managers" for broadcasts.
    public async Task JoinUserGroup(string userId, string role)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

        if (role is "Admin" or "Manager")
            await Groups.AddToGroupAsync(Context.ConnectionId, "managers");
    }
}
