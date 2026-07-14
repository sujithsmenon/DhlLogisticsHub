namespace DhlLogistics.Shared.Models;

/// <summary>Issuance state of a consolidated <see cref="CustomerInvoice"/>. Deliberately mirrors
/// <see cref="InvoiceStatus"/> so the two invoice surfaces read the same way.</summary>
public enum CustomerInvoiceStatus
{
    Issued    = 10,
    Cancelled = 20,
}

/// <summary>
/// A consolidated customer (tax) invoice raised over a <b>Billing Group</b> — the set of Bills that share
/// one <see cref="Bill.CustomerInvoiceNumber"/>.
///
/// <para>Why this exists as its own entity: elsewhere in the ERP "the Bill IS the invoice" and
/// <see cref="Bill.InvoiceNumber"/> is derived 1:1 from <see cref="Bill.BillNo"/>. A single invoice spanning
/// several bills therefore has no number of its own under that scheme, so it gets a real sequence here
/// (CI/26-27/0001) while the customer's own reference (<see cref="CustomerInvoiceNumber"/>) stays the
/// business key that links Job → Clearance Bill → Transportation Bill → this invoice.</para>
///
/// <para>It duplicates NO billing logic: charges, GST and totals still live on the Bills. The amounts below
/// are a snapshot of the sum of the included bills at issue time (so a later bill edit cannot silently
/// restate an issued invoice). Accounting is untouched — A/R is posted per Bill at <b>approval</b>
/// (AccountingService), never at invoice issuance, so consolidating bills into one PDF cannot double-post.</para>
///
/// <para>Included bills are simply the Bills whose <see cref="Bill.CustomerInvoiceId"/> points here — one
/// nullable FK, no join table. That FK is also the double-invoice guard: a Bill can belong to at most one
/// consolidated invoice, and a Bill already on one cannot be issued individually.</para>
/// </summary>
public class CustomerInvoice
{
    public long Id { get; set; }

    /// <summary>Our own sequence, independent of any bill number, e.g. "CI/26-27/0001".</summary>
    public string InvoiceNo { get; set; } = string.Empty;

    /// <summary>Indian FY starting year, e.g. 2026 = FY 2026-27. Numbering is per-FY, as for Bills.</summary>
    public int FinYear { get; set; }

    /// <summary>The customer's own invoice reference — the Billing Group key. Every included Bill (and the
    /// Jobs behind them) carries this same value. Required: an invoice cannot span an empty group.</summary>
    public string CustomerInvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow.Date;

    // ── Party / branch / currency (copied from the included bills, which must agree) ──
    public int        BillingClientId { get; set; }
    public DhlClient? BillingClient   { get; set; }

    public int?            BranchId { get; set; }
    public CompanyBranch?  Branch   { get; set; }

    public int?       CurrencyId { get; set; }
    public Currency?  Currency   { get; set; }

    // ── Snapshot totals — the SUM of the included bills at issue time. Not a re-computation:
    //    the charge/GST maths stays in BillService.RecalcTotals and is never re-implemented here.
    public decimal SubTotal    { get; set; }
    public decimal GstAmount   { get; set; }
    public decimal TotalAmount { get; set; }

    public CustomerInvoiceStatus Status { get; set; } = CustomerInvoiceStatus.Issued;

    public string?   PaymentTerms { get; set; }
    public DateTime? DueDate      { get; set; }
    public string?   Remarks      { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public string?  CreatedBy { get; set; }

    public DateTime? CancelledOn     { get; set; }
    public string?   CancelledBy     { get; set; }
    public string?   CancellationReason { get; set; }

    /// <summary>The Bills consolidated onto this invoice (inverse of <see cref="Bill.CustomerInvoiceId"/>).</summary>
    public List<Bill> Bills { get; set; } = new();

    /// <summary>Generated PDF(s) for this consolidated invoice. Reuses the existing InvoiceDocument table —
    /// no second document store. (Populated in the Phase 2 PDF work.)</summary>
    public List<InvoiceDocument> Documents { get; set; } = new();
}
