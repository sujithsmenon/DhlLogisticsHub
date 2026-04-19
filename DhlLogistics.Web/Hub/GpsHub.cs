namespace DhlLogistics.Web.Hub
{
    using Microsoft.AspNetCore.SignalR;

    public class GpsHub : Hub
    {
        // Mobile app calls this every 10 seconds
        public async Task UpdateLocation(string jobId, double lat, double lng, double speed)
        {
            // Broadcast to all dashboard viewers watching this job
            await Clients.Group($"job-{jobId}")
                .SendAsync("LocationUpdated", jobId, lat, lng, speed, DateTime.UtcNow);
        }

        // Dashboard subscribes to a specific job's GPS feed
        public async Task WatchJob(string jobId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"job-{jobId}");
        }
    }
}
