namespace DhlLogistics.Shared.Models;

public enum BillEventType
{
    Created   = 1,
    Updated   = 2,
    Submitted = 3,
    Verified  = 4,
    Approved  = 5,
    Rejected  = 6,
    Closed    = 7,
    NoteAdded = 8,
}

/// <summary>Append-only audit log for a Bill. Mirrors JobOrderEvent.</summary>
public class BillEvent
{
    public long Id { get; set; }

    public long BillId { get; set; }
    public Bill? Bill   { get; set; }

    public BillEventType EventType { get; set; }

    public string? Notes { get; set; }
    public string? Actor { get; set; }
    public DateTime At   { get; set; } = DateTime.UtcNow;
}
