namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

public class BillService
{
    private readonly AppDbContext _db;
    private readonly AuthenticationStateProvider _authProvider;

    public BillService(AppDbContext db, AuthenticationStateProvider authProvider)
    {
        _db = db;
        _authProvider = authProvider;
    }

    private async Task<string> CurrentUserAsync()
    {
        var s = await _authProvider.GetAuthenticationStateAsync();
        return s.User?.Identity?.Name ?? "system";
    }

    // ── Queries ──────────────────────────────────────────────────────────────
    private IQueryable<Bill> WithRefs() => _db.Bills
        .Include(b => b.Branch)
        .Include(b => b.BillingClient)
        .Include(b => b.Currency)
        .Include(b => b.JobOrder);

    public Task<List<Bill>> GetByModeAsync(BillMode mode) =>
        WithRefs().Where(b => b.Mode == mode).OrderByDescending(b => b.Id).ToListAsync();

    public Task<List<Bill>> GetByStatusAsync(params BillStatus[] statuses) =>
        WithRefs().Where(b => statuses.Contains(b.Status)).OrderByDescending(b => b.Id).ToListAsync();

    public Task<Bill?> GetByIdAsync(long id) =>
        WithRefs()
            .Include(b => b.Charges).ThenInclude(c => c.ChargeCode)
            .Include(b => b.Charges).ThenInclude(c => c.Sac)
            .Include(b => b.Events)
            .FirstOrDefaultAsync(b => b.Id == id);

    // ── Numbering ────────────────────────────────────────────────────────────
    public static int ComputeFinYear(DateTime d) => d.Month >= 4 ? d.Year : d.Year - 1;

    private static string Prefix(BillMode mode) => mode switch
    {
        BillMode.Clearance      => "CB",
        BillMode.Forwarding     => "FB",
        BillMode.Transportation => "TB",
        _ => "BL",
    };

    private async Task<string> NextBillNoAsync(BillMode mode, int finYear)
    {
        var prefix    = Prefix(mode);
        var fyDisplay = $"{(finYear % 100):D2}-{((finYear + 1) % 100):D2}";

        var lastNo = await _db.Bills
            .Where(b => b.Mode == mode && b.FinYear == finYear)
            .OrderByDescending(b => b.Id)
            .Select(b => b.BillNo)
            .FirstOrDefaultAsync();

        var seq = 1;
        if (!string.IsNullOrEmpty(lastNo))
        {
            var tail = lastNo.Split('/').Last();
            if (int.TryParse(tail, out var n)) seq = n + 1;
        }
        return $"{prefix}/{fyDisplay}/{seq:D4}";
    }

    // ── Totals ───────────────────────────────────────────────────────────────
    public static void RecalcTotals(Bill bill)
    {
        decimal sub = 0, gst = 0, total = 0;
        foreach (var c in bill.Charges)
        {
            c.Amount    = decimal.Round(c.Quantity * c.Rate, 2);
            c.GstAmount = decimal.Round(c.Amount * c.GstRate / 100m, 2);
            c.NetAmount = c.Amount + c.GstAmount;
            sub   += c.Amount;
            gst   += c.GstAmount;
            total += c.NetAmount;
        }
        bill.SubTotal    = sub;
        bill.GstAmount   = gst;
        bill.TotalAmount = total;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────
    public async Task<Bill> CreateAsync(Bill bill)
    {
        var user = await CurrentUserAsync();
        bill.FinYear   = ComputeFinYear(bill.BillDate);
        bill.BillNo    = await NextBillNoAsync(bill.Mode, bill.FinYear);
        bill.Status    = BillStatus.Draft;
        bill.CreatedBy = user;
        bill.CreatedOn = DateTime.UtcNow;
        RecalcTotals(bill);

        // give charges sequential DisplayOrder if unset
        int order = 1;
        foreach (var c in bill.Charges) if (c.DisplayOrder == 0) c.DisplayOrder = order++;

        _db.Bills.Add(bill);
        await _db.SaveChangesAsync();
        await LogEventAsync(bill.Id, BillEventType.Created, "Bill created as Draft.", user);
        return bill;
    }

    public async Task UpdateAsync(Bill bill)
    {
        var user = await CurrentUserAsync();
        bill.ModifiedBy = user;
        bill.ModifiedOn = DateTime.UtcNow;
        RecalcTotals(bill);

        // Header
        _db.Entry(bill).State = EntityState.Modified;

        // Replace charges (simple strategy — delete + re-add, fine for the line counts we expect)
        var existing = await _db.BillCharges.Where(c => c.BillId == bill.Id).ToListAsync();
        _db.BillCharges.RemoveRange(existing);

        int order = 1;
        foreach (var c in bill.Charges)
        {
            c.Id = 0;
            c.BillId = bill.Id;
            if (c.DisplayOrder == 0) c.DisplayOrder = order;
            order++;
            _db.BillCharges.Add(c);
        }

        await _db.SaveChangesAsync();
        await LogEventAsync(bill.Id, BillEventType.Updated, null, user);
    }

    public async Task SubmitAsync(long id)
    {
        var user = await CurrentUserAsync();
        var b = await _db.Bills.FindAsync(id) ?? throw new KeyNotFoundException();
        if (b.Status != BillStatus.Draft && b.Status != BillStatus.Rejected)
            throw new InvalidOperationException($"Cannot submit a bill in '{b.Status}' status.");

        b.Status      = BillStatus.Submitted;
        b.SubmittedBy = user;
        b.SubmittedOn = DateTime.UtcNow;
        b.RejectedBy  = null; b.RejectedOn = null; b.RejectionReason = null;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, BillEventType.Submitted, "Submitted for verification.", user);
    }

    public async Task VerifyAsync(long id, string? note = null)
    {
        var user = await CurrentUserAsync();
        var b = await _db.Bills.FindAsync(id) ?? throw new KeyNotFoundException();
        if (b.Status != BillStatus.Submitted)
            throw new InvalidOperationException($"Only Submitted bills can be verified (current: {b.Status}).");

        b.Status     = BillStatus.Verified;
        b.VerifiedBy = user;
        b.VerifiedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, BillEventType.Verified, note, user);
    }

    public async Task ApproveAsync(long id, string? note = null)
    {
        var user = await CurrentUserAsync();
        var b = await _db.Bills.FindAsync(id) ?? throw new KeyNotFoundException();
        if (b.Status != BillStatus.Verified)
            throw new InvalidOperationException($"Only Verified bills can be approved (current: {b.Status}).");

        b.Status     = BillStatus.Approved;
        b.ApprovedBy = user;
        b.ApprovedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, BillEventType.Approved, note, user);
    }

    public async Task RejectAsync(long id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Rejection reason is required.");

        var user = await CurrentUserAsync();
        var b = await _db.Bills.FindAsync(id) ?? throw new KeyNotFoundException();
        if (b.Status != BillStatus.Submitted && b.Status != BillStatus.Verified)
            throw new InvalidOperationException($"Cannot reject a bill in '{b.Status}' status.");

        b.Status          = BillStatus.Rejected;
        b.RejectedBy      = user;
        b.RejectedOn      = DateTime.UtcNow;
        b.RejectionReason = reason;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, BillEventType.Rejected, reason, user);
    }

    public async Task CloseAsync(long id, string? note = null)
    {
        var user = await CurrentUserAsync();
        var b = await _db.Bills.FindAsync(id) ?? throw new KeyNotFoundException();
        if (b.Status != BillStatus.Approved)
            throw new InvalidOperationException($"Only Approved bills can be closed (current: {b.Status}).");

        b.Status   = BillStatus.Closed;
        b.ClosedBy = user;
        b.ClosedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, BillEventType.Closed, note, user);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var b = await _db.Bills.FindAsync(id);
        if (b is null) return false;
        if (b.Status != BillStatus.Draft)
            throw new InvalidOperationException("Only Draft bills can be deleted.");

        _db.Bills.Remove(b);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Events ───────────────────────────────────────────────────────────────
    private async Task LogEventAsync(long billId, BillEventType type, string? notes, string actor)
    {
        _db.BillEvents.Add(new BillEvent
        {
            BillId    = billId,
            EventType = type,
            Notes     = notes,
            Actor     = actor,
            At        = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }
}
