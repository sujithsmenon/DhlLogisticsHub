namespace DhlLogistics.Shared.Models;

/// <summary>
/// Canonical lifecycle stage every shipment passes through, regardless of
/// whether it's an AWB / ExportJob / JobOrder. Used to group items in the
/// admin activity report into a single 4x5 grid.
///   Received  → an incoming job order from DHL
///   InTransit → cargo picked up, on the way to the port
///   AtPort    → dropped at Cochin Sea/Air port, awaiting clearance
///   Cleared   → customs clearance done
///   Completed → invoice sent / vessel loaded / job closed
/// </summary>
public enum ReportStage { Received = 0, InTransit = 1, AtPort = 2, Cleared = 3, Completed = 4 }

public enum ReportMode      { Sea = 0, Air = 1 }
public enum ReportDirection { Import = 0, Export = 1 }

public record ReportPeriod(string Label, DateTime From, DateTime To);

/// <summary>
/// One row in the activity drill-down list — a single shipment regardless of
/// which source table it came from.
/// </summary>
public class ActivityItem
{
    public string Source         { get; set; } = "";  // "AWB" | "Export" | "JobOrder"
    public string Reference      { get; set; } = "";  // HAWB no / JobReference / JobOrderNo
    public string ClientName     { get; set; } = "";
    public ReportStage Stage     { get; set; }
    public string CurrentStatus  { get; set; } = "";
    public string? Location      { get; set; }        // pickup/drop OR loadport→dischargeport
    public string? VesselName    { get; set; }
    public string? VoyageNumber  { get; set; }
    public DateTime ReceivedAt   { get; set; }
    public DateTime? LastEventAt { get; set; }
}

public class QuadrantReport
{
    public ReportMode      Mode       { get; set; }
    public ReportDirection Direction  { get; set; }
    public int Received   { get; set; }
    public int InTransit  { get; set; }
    public int AtPort     { get; set; }
    public int Cleared    { get; set; }
    public int Completed  { get; set; }
    public List<ActivityItem> Items { get; set; } = [];
    public int Total => Received + InTransit + AtPort + Cleared + Completed;
}

public class ActivityReport
{
    public ReportPeriod Period { get; set; } = new("", DateTime.UtcNow, DateTime.UtcNow);
    public List<QuadrantReport> Quadrants { get; set; } = [];

    // Roll-up across all four quadrants
    public int TotalReceived  => Quadrants.Sum(q => q.Received);
    public int TotalInTransit => Quadrants.Sum(q => q.InTransit);
    public int TotalAtPort    => Quadrants.Sum(q => q.AtPort);
    public int TotalCleared   => Quadrants.Sum(q => q.Cleared);
    public int TotalCompleted => Quadrants.Sum(q => q.Completed);
    public int GrandTotal     => Quadrants.Sum(q => q.Total);
}
