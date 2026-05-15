namespace DhlLogistics.Shared.Models;

public enum JobOrderEventType
{
    Created          = 1,
    Updated          = 2,
    Submitted        = 3,
    Verified         = 4,
    Approved         = 5,
    Rejected         = 6,
    Closed           = 7,
    Reopened         = 8,
    PostVerifyEdited = 9,
    NoteAdded        = 10,
}

/// <summary>
/// Append-only audit log for a JobOrder. Mirrors AwbShipment's ShipmentEvent pattern.
/// </summary>
public class JobOrderEvent
{
    public long Id { get; set; }

    public long JobOrderId { get; set; }
    public JobOrder? JobOrder { get; set; }

    public JobOrderEventType EventType { get; set; }

    public string? Notes { get; set; }
    public string? Actor { get; set; }     // user name / id
    public DateTime At   { get; set; } = DateTime.UtcNow;
}
