namespace DhlLogistics.Shared.Models;

/// <summary>
/// The Indian financial year — the ONE definition, used by every numbering sequence in the ERP
/// (bill numbers, job numbers, voucher numbers, customer-invoice numbers).
///
/// <para>Before this, <c>ComputeFinYear</c> was copied verbatim into BillService, JobOrderService and
/// VoucherService, and the "26-27" display format was re-written inline in four more places. Every one of
/// those numbers is a legal document reference: if the copies had ever drifted, two modules would have
/// disagreed about which financial year a document belonged to, and the divergence would have been invisible
/// until an auditor found it.</para>
/// </summary>
public static class FinancialYear
{
    /// <summary>The FY a date falls in, named by its STARTING year: the Indian FY runs April→March, so
    /// 15-Mar-2027 belongs to FY 2026 (i.e. "2026-27"), not 2027.</summary>
    public static int Of(DateTime date) => date.Month >= 4 ? date.Year : date.Year - 1;

    /// <summary>Two-digit display form used inside document numbers — 2026 → "26-27".</summary>
    public static string Display(int startYear) =>
        $"{(startYear % 100):D2}-{((startYear + 1) % 100):D2}";

    /// <summary>Display form for the FY a date falls in — 15-Mar-2027 → "26-27".</summary>
    public static string DisplayFor(DateTime date) => Display(Of(date));
}
