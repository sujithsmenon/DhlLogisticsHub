namespace DhlLogistics.Shared.Models;

public enum VoucherEventType
{
    Created   = 1,
    Updated   = 2,
    Submitted = 3,
    Verified  = 4,
    Approved  = 5,
    Rejected  = 6,
    Posted    = 7,
    NoteAdded = 8,
}

/// <summary>Append-only audit log for a Voucher. Mirrors JobOrderEvent.</summary>
public class VoucherEvent
{
    public long Id { get; set; }

    public long VoucherId { get; set; }
    public Voucher? Voucher { get; set; }

    public VoucherEventType EventType { get; set; }

    public string? Notes { get; set; }
    public string? Actor { get; set; }
    public DateTime At   { get; set; } = DateTime.UtcNow;
}
