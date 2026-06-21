namespace DhlLogistics.Shared.Models;

public class DailyReportData
{
    public int TotalActiveContainers { get; set; }
    public int TotalShippedThisMonth { get; set; }
    public int TotalPendingPickups { get; set; }
    public int TotalActiveClients { get; set; }
    public int TotalInTransit { get; set; }
    public int TotalActiveExecutives { get; set; }
}
