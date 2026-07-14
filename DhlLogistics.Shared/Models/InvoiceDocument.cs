namespace DhlLogistics.Shared.Models;

/// <summary>
/// Type of financial document attached to a Bill. The Bill itself is the customer
/// invoice; this enum lets the same table also hold vendor invoices and credit /
/// debit notes so those future flows need no schema redesign.
/// </summary>
public enum InvoiceDocumentType
{
    CustomerInvoice = 1,   // generated from Bill data (our outgoing invoice)
    VendorInvoice   = 2,   // uploaded — supplier's invoice against this job/bill
    CreditNote      = 3,
    DebitNote       = 4,
    Other           = 9,
}

/// <summary>
/// A generated or uploaded document file linked to a <see cref="Bill"/>.
/// The ONLY new invoice table — no duplicate charges / totals / GST / party data.
/// </summary>
public class InvoiceDocument
{
    public long Id { get; set; }

    public long  BillId { get; set; }
    public Bill? Bill   { get; set; }

    /// <summary>Set when this document is the PDF of a consolidated <see cref="CustomerInvoice"/> (Billing
    /// Group) rather than of a single Bill. BillId still points at the anchor bill, so every existing query
    /// that filters by BillId keeps working untouched. Null for all existing rows.</summary>
    public long?            CustomerInvoiceId { get; set; }
    public CustomerInvoice? CustomerInvoice   { get; set; }

    public InvoiceDocumentType DocumentType { get; set; } = InvoiceDocumentType.CustomerInvoice;

    /// <summary>Stored file name on disk (unique, e.g. a GUID + extension).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Original name as uploaded / the friendly invoice file name.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Legacy path (relative to the invoice storage root) where the file lives on
    /// disk. Kept for backward compatibility; <see cref="Content"/> is now the source of truth.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>The file bytes, stored in the DB so the document is available on any server
    /// instance / environment (the app filesystem on EB is per-instance and not persistent).
    /// Nullable only for legacy rows created before this column existed (served from disk).</summary>
    public byte[]? Content { get; set; }

    public string ContentType { get; set; } = "application/pdf";

    public string?  UploadedBy   { get; set; }
    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Incrementing version per Bill — re-issuing an invoice bumps this.</summary>
    public int  Version  { get; set; } = 1;

    /// <summary>Superseded documents are kept for audit but flagged inactive.</summary>
    public bool IsActive { get; set; } = true;
}
